using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantSubscriptionsAndPlanLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanLimits",
                columns: table => new
                {
                    Tier = table.Column<int>(type: "int", nullable: false),
                    TenantsPerUser = table.Column<int>(type: "int", nullable: true),
                    WorkspacesPerUser = table.Column<int>(type: "int", nullable: true),
                    DocumentsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    StoragePerUserGb = table.Column<int>(type: "int", nullable: true),
                    StoragePerTenantGb = table.Column<int>(type: "int", nullable: true),
                    StoragePerWorkspaceGb = table.Column<int>(type: "int", nullable: true),
                    ProjectsPerWorkspace = table.Column<int>(type: "int", nullable: true),
                    LlmProvidersPerUser = table.Column<int>(type: "int", nullable: true),
                    EnableOcr = table.Column<bool>(type: "bit", nullable: false),
                    EnableRagIndexing = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanTier = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                unique: true);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [PlanLimits] WHERE [Tier] = 0)
                BEGIN
                    INSERT INTO [PlanLimits]
                    ([Tier], [TenantsPerUser], [WorkspacesPerUser], [DocumentsPerWorkspace],
                     [StoragePerUserGb], [StoragePerTenantGb], [StoragePerWorkspaceGb],
                     [ProjectsPerWorkspace], [LlmProvidersPerUser], [EnableOcr], [EnableRagIndexing], [CreatedAt], [UpdatedAt])
                    VALUES
                    (0, 1, 3, 200, 2, 5, 2, 10, 3, 1, 1, SYSDATETIMEOFFSET(), NULL),
                    (1, 5, 50, 5000, 50, 200, 100, 500, 30, 1, 1, SYSDATETIMEOFFSET(), NULL),
                    (2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 1, SYSDATETIMEOFFSET(), NULL);
                END
                """);

            migrationBuilder.Sql("""
                INSERT INTO [Subscriptions] ([Id], [TenantId], [PlanTier], [IsActive], [StartsAt], [EndsAt], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), t.[Id], 0, 1, SYSDATETIMEOFFSET(), NULL, SYSDATETIMEOFFSET(), NULL
                FROM [Tenants] t
                LEFT JOIN [Subscriptions] s ON s.[TenantId] = t.[Id]
                WHERE s.[TenantId] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanLimits");

            migrationBuilder.DropTable(
                name: "Subscriptions");
        }
    }
}
