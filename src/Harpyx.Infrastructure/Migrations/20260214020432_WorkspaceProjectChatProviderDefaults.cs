using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceProjectChatProviderDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChatProviderId",
                table: "Workspaces",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChatLlmEnabled",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ChatLlmOverride",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ChatProviderId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_ChatProviderId",
                table: "Workspaces",
                column: "ChatProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ChatProviderId",
                table: "Projects",
                column: "ChatProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserLlmProviders_ChatProviderId",
                table: "Projects",
                column: "ChatProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserLlmProviders_ChatProviderId",
                table: "Workspaces",
                column: "ChatProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserLlmProviders_ChatProviderId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserLlmProviders_ChatProviderId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_ChatProviderId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ChatProviderId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ChatProviderId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "IsChatLlmEnabled",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "ChatLlmOverride",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ChatProviderId",
                table: "Projects");
        }
    }
}
