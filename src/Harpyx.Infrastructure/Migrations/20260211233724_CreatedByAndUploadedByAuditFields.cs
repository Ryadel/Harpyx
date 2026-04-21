using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreatedByAndUploadedByAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Workspaces",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill creator on legacy personal tenants where exactly one user is assigned.
            migrationBuilder.Sql(
                """
                UPDATE t
                SET t.[CreatedByUserId] = ut.[UserId]
                FROM [Tenants] t
                INNER JOIN (
                    SELECT [TenantId], MAX([UserId]) AS [UserId]
                    FROM [UserTenants]
                    GROUP BY [TenantId]
                    HAVING COUNT(*) = 1
                ) ut ON ut.[TenantId] = t.[Id]
                WHERE t.[IsPersonal] = 1
                  AND t.[CreatedByUserId] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_CreatedByUserId",
                table: "Workspaces",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedByUserId",
                table: "Tenants",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByUserId",
                table: "Documents",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UploadedByUserId",
                table: "Documents",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Users_CreatedByUserId",
                table: "Tenants",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_Users_CreatedByUserId",
                table: "Workspaces",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UploadedByUserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Users_CreatedByUserId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_Users_CreatedByUserId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_CreatedByUserId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CreatedByUserId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UploadedByUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "Documents");
        }
    }
}
