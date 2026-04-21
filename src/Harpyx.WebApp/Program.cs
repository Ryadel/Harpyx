var builder = WebApplication.CreateBuilder(args);

builder
    .ConfigureHarpyxHost()
    .AddHarpyxWebAppServices();

var app = builder.Build();

app.UseHarpyxWebAppPipeline();
await app.InitializeHarpyxDatabaseAndSeedAsync(builder.Configuration);

app.Run();
