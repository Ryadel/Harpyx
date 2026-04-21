using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformUsageLimitsOpenSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformUsageLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantsPerUser = table.Column<int>(type: "int", nullable: true),
                    WorkspacesPerUser = table.Column<int>(type: "int", nullable: true),
                    DocumentsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    StoragePerUserGb = table.Column<int>(type: "int", nullable: true),
                    StoragePerTenantGb = table.Column<int>(type: "int", nullable: true),
                    StoragePerWorkspaceGb = table.Column<int>(type: "int", nullable: true),
                    ProjectsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    PermanentProjectsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    MaxTemporaryProjectLifetimeHours = table.Column<int>(type: "int", nullable: true),
                    LlmProvidersPerUser = table.Column<int>(type: "int", nullable: true),
                    EnableOcr = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableRagIndexing = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableApi = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsageLimits", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[PlanLimits]', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [PlatformUsageLimits] (
                        [Id],
                        [TenantsPerUser],
                        [WorkspacesPerUser],
                        [DocumentsPerWorkspace],
                        [StoragePerUserGb],
                        [StoragePerTenantGb],
                        [StoragePerWorkspaceGb],
                        [ProjectsPerWorkspace],
                        [PermanentProjectsPerWorkspace],
                        [MaxTemporaryProjectLifetimeHours],
                        [LlmProvidersPerUser],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [EnableApi],
                        [CreatedAt],
                        [UpdatedAt])
                    SELECT TOP(1)
                        NEWID(),
                        [TenantsPerUser],
                        [WorkspacesPerUser],
                        [DocumentsPerWorkspace],
                        [StoragePerUserGb],
                        [StoragePerTenantGb],
                        [StoragePerWorkspaceGb],
                        [ProjectsPerWorkspace],
                        [PermanentProjectsPerWorkspace],
                        [MaxTemporaryProjectLifetimeHours],
                        [LlmProvidersPerUser],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [EnableApi],
                        SYSUTCDATETIME(),
                        NULL
                    FROM [PlanLimits]
                    WHERE [Tier] = 0
                    ORDER BY [Tier];
                END

                IF NOT EXISTS (SELECT 1 FROM [PlatformUsageLimits])
                BEGIN
                    INSERT INTO [PlatformUsageLimits] (
                        [Id],
                        [TenantsPerUser],
                        [WorkspacesPerUser],
                        [DocumentsPerWorkspace],
                        [StoragePerUserGb],
                        [StoragePerTenantGb],
                        [StoragePerWorkspaceGb],
                        [ProjectsPerWorkspace],
                        [PermanentProjectsPerWorkspace],
                        [MaxTemporaryProjectLifetimeHours],
                        [LlmProvidersPerUser],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [EnableApi],
                        [CreatedAt],
                        [UpdatedAt])
                    VALUES (
                        NEWID(),
                        1,
                        3,
                        200,
                        2,
                        5,
                        2,
                        10,
                        3,
                        720,
                        3,
                        CAST(1 AS bit),
                        CAST(1 AS bit),
                        CAST(1 AS bit),
                        SYSUTCDATETIME(),
                        NULL);
                END
                """);

            migrationBuilder.DropTable(
                name: "PlanLimits");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingEnabled",
                table: "PlatformSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BillingEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlanLimits",
                columns: table => new
                {
                    Tier = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DocumentsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    EnableApi = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableOcr = table.Column<bool>(type: "bit", nullable: false),
                    EnableRagIndexing = table.Column<bool>(type: "bit", nullable: false),
                    LlmProvidersPerUser = table.Column<int>(type: "int", nullable: true),
                    MaxTemporaryProjectLifetimeHours = table.Column<int>(type: "int", nullable: true),
                    PermanentProjectsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    ProjectsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    StoragePerTenantGb = table.Column<int>(type: "int", nullable: true),
                    StoragePerUserGb = table.Column<int>(type: "int", nullable: true),
                    StoragePerWorkspaceGb = table.Column<int>(type: "int", nullable: true),
                    TenantsPerUser = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WorkspacesPerUser = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanLimits", x => x.Tier);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PlanTier = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_IsActive",
                table: "Subscriptions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[PlatformUsageLimits]', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [PlanLimits] (
                        [Tier],
                        [CreatedAt],
                        [DocumentsPerWorkspace],
                        [EnableApi],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [LlmProvidersPerUser],
                        [MaxTemporaryProjectLifetimeHours],
                        [PermanentProjectsPerWorkspace],
                        [ProjectsPerWorkspace],
                        [StoragePerTenantGb],
                        [StoragePerUserGb],
                        [StoragePerWorkspaceGb],
                        [TenantsPerUser],
                        [UpdatedAt],
                        [WorkspacesPerUser])
                    SELECT TOP(1)
                        0,
                        SYSUTCDATETIME(),
                        [DocumentsPerWorkspace],
                        [EnableApi],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [LlmProvidersPerUser],
                        [MaxTemporaryProjectLifetimeHours],
                        [PermanentProjectsPerWorkspace],
                        [ProjectsPerWorkspace],
                        [StoragePerTenantGb],
                        [StoragePerUserGb],
                        [StoragePerWorkspaceGb],
                        [TenantsPerUser],
                        NULL,
                        [WorkspacesPerUser]
                    FROM [PlatformUsageLimits]
                    ORDER BY [CreatedAt];
                END

                IF NOT EXISTS (SELECT 1 FROM [PlanLimits] WHERE [Tier] = 0)
                BEGIN
                    INSERT INTO [PlanLimits] (
                        [Tier],
                        [CreatedAt],
                        [DocumentsPerWorkspace],
                        [EnableApi],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [LlmProvidersPerUser],
                        [MaxTemporaryProjectLifetimeHours],
                        [PermanentProjectsPerWorkspace],
                        [ProjectsPerWorkspace],
                        [StoragePerTenantGb],
                        [StoragePerUserGb],
                        [StoragePerWorkspaceGb],
                        [TenantsPerUser],
                        [UpdatedAt],
                        [WorkspacesPerUser])
                    VALUES (0, SYSUTCDATETIME(), 200, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), 3, 720, 3, 10, 5, 2, 2, 1, NULL, 3);
                END

                IF NOT EXISTS (SELECT 1 FROM [PlanLimits] WHERE [Tier] = 1)
                BEGIN
                    INSERT INTO [PlanLimits] (
                        [Tier],
                        [CreatedAt],
                        [DocumentsPerWorkspace],
                        [EnableApi],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [LlmProvidersPerUser],
                        [MaxTemporaryProjectLifetimeHours],
                        [PermanentProjectsPerWorkspace],
                        [ProjectsPerWorkspace],
                        [StoragePerTenantGb],
                        [StoragePerUserGb],
                        [StoragePerWorkspaceGb],
                        [TenantsPerUser],
                        [UpdatedAt],
                        [WorkspacesPerUser])
                    VALUES (1, SYSUTCDATETIME(), 5000, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), 30, NULL, 10, 500, 200, 50, 100, 5, NULL, 50);
                END

                IF NOT EXISTS (SELECT 1 FROM [PlanLimits] WHERE [Tier] = 2)
                BEGIN
                    INSERT INTO [PlanLimits] (
                        [Tier],
                        [CreatedAt],
                        [DocumentsPerWorkspace],
                        [EnableApi],
                        [EnableOcr],
                        [EnableRagIndexing],
                        [LlmProvidersPerUser],
                        [MaxTemporaryProjectLifetimeHours],
                        [PermanentProjectsPerWorkspace],
                        [ProjectsPerWorkspace],
                        [StoragePerTenantGb],
                        [StoragePerUserGb],
                        [StoragePerWorkspaceGb],
                        [TenantsPerUser],
                        [UpdatedAt],
                        [WorkspacesPerUser])
                    VALUES (2, SYSUTCDATETIME(), NULL, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
                END
                """);

            migrationBuilder.DropTable(
                name: "PlatformUsageLimits");
        }
    }
}
