using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceProjectLlmOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOcrLlmEnabled",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRagLlmEnabled",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OcrProviderId",
                table: "Workspaces",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RagEmbeddingProviderId",
                table: "Workspaces",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrLlmOverride",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OcrProviderId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RagLlmOverride",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_OcrProviderId",
                table: "Workspaces",
                column: "OcrProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_RagEmbeddingProviderId",
                table: "Workspaces",
                column: "RagEmbeddingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OcrProviderId",
                table: "Projects",
                column: "OcrProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserLlmProviders_OcrProviderId",
                table: "Projects",
                column: "OcrProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserLlmProviders_OcrProviderId",
                table: "Workspaces",
                column: "OcrProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserLlmProviders_RagEmbeddingProviderId",
                table: "Workspaces",
                column: "RagEmbeddingProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserLlmProviders_OcrProviderId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserLlmProviders_OcrProviderId",
                table: "Workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserLlmProviders_RagEmbeddingProviderId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_OcrProviderId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_RagEmbeddingProviderId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OcrProviderId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsOcrLlmEnabled",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "IsRagLlmEnabled",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "OcrProviderId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "RagEmbeddingProviderId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "OcrLlmOverride",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OcrProviderId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RagLlmOverride",
                table: "Projects");
        }
    }
}
