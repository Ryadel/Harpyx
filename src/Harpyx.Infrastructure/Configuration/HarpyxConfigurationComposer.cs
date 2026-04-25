using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Harpyx.Infrastructure.Configuration;

public static class HarpyxConfigurationComposer
{
    public static void ComposeDerivedValues(IConfigurationManager configuration)
    {
        ComposeDatabaseConnectionString(configuration);
        ComposeEntraIdAuthority(configuration);
    }

    private static void ComposeDatabaseConnectionString(IConfigurationManager configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Harpyx")))
        {
            return;
        }

        var host = configuration["Database:Host"];
        var name = configuration["Database:Name"];
        var username = configuration["Database:Username"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var port = configuration.GetValue<int?>("Database:Port");
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = port is > 0 ? $"{host},{port}" : host,
            InitialCatalog = name,
            UserID = username,
            Password = configuration["Database:Password"] ?? string.Empty,
            Encrypt = configuration.GetValue<bool?>("Database:Encrypt") ?? true,
            TrustServerCertificate = configuration.GetValue<bool?>("Database:TrustServerCertificate") ?? true
        };

        configuration["ConnectionStrings:Harpyx"] = builder.ConnectionString;
    }

    private static void ComposeEntraIdAuthority(IConfigurationManager configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["Authentication:EntraId:Authority"]))
        {
            return;
        }

        var instance = configuration["Authentication:EntraId:Instance"];
        var tenantId = configuration["Authentication:EntraId:TenantId"];

        if (string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        configuration["Authentication:EntraId:Authority"] = $"{instance.TrimEnd('/')}/{tenantId}";
    }
}
