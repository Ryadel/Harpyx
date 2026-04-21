using Harpyx.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Harpyx.Infrastructure.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static TBuilder ConfigureHarpyxHost<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        HarpyxEnvironmentFileLoader.Load(builder.Environment.ContentRootPath);
        builder.Configuration.AddEnvironmentVariables();
        HarpyxConfigurationComposer.ComposeDerivedValues(builder.Configuration);

        builder.Services.AddSerilog((_, config) =>
            config.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

        return builder;
    }
}
