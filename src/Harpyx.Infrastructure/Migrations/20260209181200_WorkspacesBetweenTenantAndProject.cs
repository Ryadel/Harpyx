using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkspacesBetweenTenantAndProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Projects",
                newName: "WorkspaceId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                newName: "IX_Projects_WorkspaceId");

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspaces_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_TenantId_Name",
                table: "Workspaces",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [Workspaces] ([Id], [TenantId], [Name], [Description], [IsActive], [CreatedAt], [UpdatedAt])
                SELECT [t].[Id], [t].[Id], N'Default', NULL, CAST(1 AS bit), SYSUTCDATETIME(), NULL
                FROM [Tenants] AS [t]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [Workspaces] AS [w]
                    WHERE [w].[Id] = [t].[Id]
                );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Workspaces_WorkspaceId",
                table: "Projects",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Workspaces_WorkspaceId",
                table: "Projects");

            migrationBuilder.Sql("""
                UPDATE [p]
                SET [p].[WorkspaceId] = [w].[TenantId]
                FROM [Projects] AS [p]
                INNER JOIN [Workspaces] AS [w] ON [w].[Id] = [p].[WorkspaceId];
                """);

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Projects",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_WorkspaceId",
                table: "Projects",
                newName: "IX_Projects_TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
