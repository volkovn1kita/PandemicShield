using Microsoft.EntityFrameworkCore;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using PandemicShield.Api.Endpoints;
using Microsoft.AspNetCore.Http.Features;

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

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = null;
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapThreatEndpoints();
            app.MapDictionaryEndpoints();
            app.MapAnalysisEndpoints();

            app.Run();
        }
    }
}
