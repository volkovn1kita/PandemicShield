using Microsoft.EntityFrameworkCore;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;

namespace PandemicShield.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<PandemicDbContext>();

            builder.Services.AddOpenApi();

            builder.Services.AddGrpcClient<DnaParserService.DnaParserServiceClient>(o =>
            {
                o.Address = new Uri("https://localhost:56627");
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                return handler;
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapGet("/api/threats", async (PandemicDbContext db) =>
            {
                var threats = await db.Threats.ToListAsync();
                return Results.Ok(threats);
            });

            app.MapPost("/api/dictionary", async (ReferenceMutationEntity mutation, PandemicDbContext db) =>
            {
                db.Mutation.Add(mutation);
                await db.SaveChangesAsync();
                return Results.Ok();
            });

            app.MapGet("/api/dictionary", async (PandemicDbContext db) =>
            {
                var mutations = await db.Mutation.ToListAsync();
                return Results.Ok(mutations);
            });

            app.MapPost("/api/analyze", async (
                IFormFile file,
                [FromForm] ThreatCategory category,
                DnaParserService.DnaParserServiceClient grpcClient) => // <-- API автоматично дасть нам клієнта
                        {
                            if (file == null || file.Length == 0)
                                return Results.BadRequest("Файл порожній.");

                            // 1. Відкриваємо канал зв'язку з Парсером
                            using var call = grpcClient.StreamDnaForParsing();

                            // 2. Відкриваємо файл як ПОТІК (не завантажуючи його повністю в пам'ять!)
                            using var stream = file.OpenReadStream();

                            // Створюємо "ківш" (буфер) на 32 Кілобайти. Ми будемо черпати файл цим ковшем.
                            byte[] buffer = new byte[32 * 1024];
                            int bytesRead;

                            // Читаємо файл ковшами, поки він не закінчиться (> 0)
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                // gRPC має свій власний тип для байтів, тому копіюємо наш ківш у формат gRPC
                                var byteString = ByteString.CopyFrom(buffer, 0, bytesRead);

                                // Формуємо посилку
                                var request = new DnaChunkRequest
                                {
                                    ChunkData = byteString,
                                    CategoryId = (int)category
                                };

                                // ВІДПРАВЛЯЄМО шматок у трубу Парсеру!
                                await call.RequestStream.WriteAsync(request);
                            }

                            // 3. Файл закінчився. Кажемо Парсеру: "Я все відправив, закривай трубу"
                            await call.RequestStream.CompleteAsync();

                            // 4. Чекаємо на фінальну відповідь від Парсера
                            var response = await call.ResponseAsync;

                            // Віддаємо результат користувачу
                            return Results.Ok(new
                            {
                                Message = response.Message,
                                ChunksProcessed = response.ChunkProcessed
                            });
                        })
            .DisableAntiforgery();


            app.Run();
        }
    }
}
