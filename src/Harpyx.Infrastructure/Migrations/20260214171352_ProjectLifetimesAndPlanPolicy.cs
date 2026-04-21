using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectLifetimesAndPlanPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoExtendLifetimeOnActivity",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LifetimeExpiresAtUtc",
                table: "Projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifetimePreset",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTemporaryProjectLifetimeHours",
                table: "PlanLimits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PermanentProjectsPerWorkspace",
                table: "PlanLimits",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LifetimeExpiresAtUtc",
                table: "Projects",
                column: "LifetimeExpiresAtUtc");

            migrationBuilder.Sql("""
                UPDATE [PlanLimits]
                SET [PermanentProjectsPerWorkspace] = CASE
                        WHEN [Tier] = 0 THEN COALESCE([PermanentProjectsPerWorkspace], 3)
                        WHEN [Tier] = 1 THEN COALESCE([PermanentProjectsPerWorkspace], 10)
                        ELSE NULL
                    END,
                    [MaxTemporaryProjectLifetimeHours] = CASE
                        WHEN [Tier] = 0 THEN COALESCE([MaxTemporaryProjectLifetimeHours], 720)
                        ELSE NULL
                    END
                WHERE [Tier] IN (0, 1, 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_LifetimeExpiresAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AutoExtendLifetimeOnActivity",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LifetimeExpiresAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LifetimePreset",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "MaxTemporaryProjectLifetimeHours",
                table: "PlanLimits");

            migrationBuilder.DropColumn(
                name: "PermanentProjectsPerWorkspace",
                table: "PlanLimits");
        }
    }
}
