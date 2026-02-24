using PandemicShield.Parser.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<GrpcParserService>();

app.Run();