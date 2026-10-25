using Microsoft.EntityFrameworkCore;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using PandemicShield.Api.Endpoints;
using PandemicShield.Api.Extensions;
using PandemicShield.Api.Imports;
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

            builder.Services.AddSingleton(AnalysisOptions.FromEnvironment());
            builder.Services.AddSingleton<JobPublisher>();
            builder.Services.AddScoped<AnalysisSubmitter>();

            // Імпорт відкритих даних NCBI: завантаження у фоні, щоб запит не чекав на сотні збірок.
            builder.Services.AddHttpClient<NcbiClient>(client => client.Timeout = TimeSpan.FromMinutes(10));
            builder.Services.AddHostedService<ImportWorker>();

            var parserUrl = builder.Configuration["ParserUrl"] ?? "https://localhost:56627";

            builder.Services.AddGrpcClient<DnaParserService.DnaParserServiceClient>(o =>
            {
                o.Address = new Uri(parserUrl);
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

            PandemicShield.Api.Extensions.DbSeeder.Seed();

            app.MapThreatEndpoints();
            app.MapDictionaryEndpoints();
            app.MapAnalysisEndpoints();
            app.MapNextcladeEndpoints();
            app.MapAmrEndpoints();
            app.MapImportEndpoints();
            app.MapSurveillanceEndpoints();
            app.MapReportEndpoints();

            app.Run();
        }
    }
}
