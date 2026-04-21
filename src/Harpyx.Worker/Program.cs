using Harpyx.Infrastructure.Extensions;
using Harpyx.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureHarpyxHost();
builder.AddHarpyxWorkerServices();

var host = builder.Build();

await host.RunAsync();
