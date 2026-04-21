namespace Harpyx.WebApp.Extensions;

public static class WebApplicationInitializationExtensions
{
    public static async Task InitializeHarpyxDatabaseAndSeedAsync(this WebApplication app, IConfiguration configuration)
    {
        var applyMigrationsOnStartup = configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
            ?? app.Environment.IsDevelopment();

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Database");

        if (applyMigrationsOnStartup)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HarpyxDbContext>();

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(CancellationToken.None);
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(CancellationToken.None);

            logger.LogInformation("EF Provider: {Provider}", dbContext.Database.ProviderName);
            logger.LogInformation("Applied migrations ({Count}): {Migrations}", appliedMigrations.Count(), string.Join(", ", appliedMigrations));
            logger.LogInformation("Pending migrations ({Count}): {Migrations}", pendingMigrations.Count(), string.Join(", ", pendingMigrations));

            await dbContext.Database.MigrateAsync(CancellationToken.None);
        }
        else
        {
            logger.LogInformation("Database migrations on startup are disabled.");
        }

        var seedAdmins = GetSeedAdminUsers(configuration);
        if (seedAdmins.Count == 0)
        {
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var hasChanges = false;
        foreach (var seedAdmin in seedAdmins)
        {
            hasChanges |= await SeedAdminUserAsync(users, logger, seedAdmin, CancellationToken.None);
        }

        if (hasChanges)
        {
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static IReadOnlyList<SeedAdminUserOptions> GetSeedAdminUsers(IConfiguration configuration)
    {
        var result = configuration
            .GetSection("SeedOptions:AdminUsers")
            .GetChildren()
            .Select(section => new SeedAdminUserOptions
            {
                Provider = section["Provider"],
                UniqueId = section["UniqueId"],
                Email = section["Email"]
            })
            .Where(seedAdmin => !seedAdmin.IsEmpty)
            .ToList();

        var legacyObjectId = configuration["SeedAdmin:ObjectId"];
        var legacyEmail = configuration["SeedAdmin:Email"];
        if (!string.IsNullOrWhiteSpace(legacyObjectId) && !string.IsNullOrWhiteSpace(legacyEmail))
        {
            result.Add(new SeedAdminUserOptions
            {
                Provider = SeedAdminProviders.EntraId,
                UniqueId = legacyObjectId,
                Email = legacyEmail
            });
        }

        return result;
    }

    private static async Task<bool> SeedAdminUserAsync(
        IUserRepository users,
        Microsoft.Extensions.Logging.ILogger logger,
        SeedAdminUserOptions seedAdmin,
        CancellationToken cancellationToken)
    {
        var provider = seedAdmin.Provider?.Trim();
        var uniqueId = seedAdmin.UniqueId?.Trim();
        var email = seedAdmin.Email?.Trim();

        if (string.IsNullOrWhiteSpace(provider) ||
            string.IsNullOrWhiteSpace(uniqueId) ||
            string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Skipping incomplete seed admin entry. Provider, UniqueId, and Email are required.");
            return false;
        }

        var identityKind = GetIdentityKind(provider);
        if (identityKind is null)
        {
            logger.LogWarning("Skipping seed admin {Email}: unsupported provider {Provider}.", email, provider);
            return false;
        }

        var existingUser = identityKind == SeedAdminIdentityKind.ObjectId
            ? await users.GetByObjectIdAsync(uniqueId, cancellationToken)
            : await users.GetBySubjectIdAsync(uniqueId, cancellationToken);

        existingUser ??= await users.GetByEmailAsync(email, cancellationToken);

        if (existingUser is null)
        {
            await users.AddAsync(new User
            {
                ObjectId = identityKind == SeedAdminIdentityKind.ObjectId ? uniqueId : null,
                SubjectId = identityKind == SeedAdminIdentityKind.SubjectId ? uniqueId : null,
                Email = email,
                Role = UserRole.Admin,
                IsActive = true
            }, cancellationToken);

            logger.LogInformation("Seeded admin user {Email} for provider {Provider}.", email, provider);
            return true;
        }

        var updated = false;
        if (identityKind == SeedAdminIdentityKind.ObjectId && existingUser.ObjectId != uniqueId)
        {
            existingUser.ObjectId = uniqueId;
            updated = true;
        }

        if (identityKind == SeedAdminIdentityKind.SubjectId && existingUser.SubjectId != uniqueId)
        {
            existingUser.SubjectId = uniqueId;
            updated = true;
        }

        if (!string.Equals(existingUser.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            existingUser.Email = email;
            updated = true;
        }

        if (existingUser.Role != UserRole.Admin)
        {
            existingUser.Role = UserRole.Admin;
            updated = true;
        }

        if (!existingUser.IsActive)
        {
            existingUser.IsActive = true;
            updated = true;
        }

        if (updated)
        {
            existingUser.UpdatedAt = DateTimeOffset.UtcNow;
            users.Update(existingUser);
            logger.LogInformation("Updated seed admin user {Email} for provider {Provider}.", email, provider);
        }

        return updated;
    }

    private static SeedAdminIdentityKind? GetIdentityKind(string provider)
    {
        var normalizedProvider = provider.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalizedProvider.ToLowerInvariant() switch
        {
            "entraid" or "microsoftentraid" or "azuread" or "azureactivedirectory" or "openidconnect" =>
                SeedAdminIdentityKind.ObjectId,
            "google" =>
                SeedAdminIdentityKind.SubjectId,
            _ => null
        };
    }

    private static class SeedAdminProviders
    {
        public const string EntraId = "EntraId";
    }

    private enum SeedAdminIdentityKind
    {
        ObjectId,
        SubjectId
    }

    private sealed class SeedAdminUserOptions
    {
        public string? Provider { get; init; }
        public string? UniqueId { get; init; }
        public string? Email { get; init; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Provider) &&
            string.IsNullOrWhiteSpace(UniqueId) &&
            string.IsNullOrWhiteSpace(Email);
    }
}
