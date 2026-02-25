using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;

namespace PandemicShield.Api.Endpoints
{
    public static class AnalysisEndpoints
    {

        public static void MapAnalysisEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/analyze", async (
                [FromForm(Name = "file")] IFormFile file,
                [FromForm] ThreatCategory category,
                DnaParserService.DnaParserServiceClient grpcClient) =>
            {
                if (file == null || file.Length == 0)
                    return Results.BadRequest("Файл порожній або не вибрано");

                var allowedExtensions = new[] { ".fasta", ".fa", ".fna", ".txt" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                    return Results.BadRequest("Неправильний формат файлу. Дозволені лише FASTA файли (.fasta, .fa, .txt).");

                using var peekStream = new StreamReader(file.OpenReadStream());
                var firstLine = await peekStream.ReadLineAsync();

                if (firstLine == null || !firstLine.StartsWith('>'))
                    return Results.BadRequest("\"Файл пошкоджений або не є валідним FASTA-форматом (має починатися з '>').");

                peekStream.BaseStream.Position = 0;

                using var call = grpcClient.StreamDnaForParsing();
                using var stream = file.OpenReadStream();

                byte[] buffer = new byte[32 * 1024];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var byteString = ByteString.CopyFrom(buffer, 0, bytesRead);

                    var request = new DnaChunkRequest
                    {
                        ChunkData = byteString,
                        CategoryId = (int)category
                    };

                    await call.RequestStream.WriteAsync(request);
                }

                await call.RequestStream.CompleteAsync();

                var response = await call.ResponseAsync;

                return Results.Ok(new
                {
                    Message = response.Message,
                    ChunkProcessed = response.ChunkProcessed
                });
            })
            .DisableAntiforgery();
        }
    }
}
