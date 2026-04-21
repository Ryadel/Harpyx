using System;
using Harpyx.Application.Defaults;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectPromptHistoryAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultSystemPrompt",
                table: "PlatformSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: PromptDefaults.DefaultSystemPrompt);

            migrationBuilder.AddColumn<int>(
                name: "SystemPromptHistoryLimitPerProject",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: PromptDefaults.SystemPromptHistoryLimitPerProject);

            migrationBuilder.AddColumn<int>(
                name: "SystemPromptMaxLengthChars",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: PromptDefaults.SystemPromptMaxLengthChars);

            migrationBuilder.AddColumn<int>(
                name: "UserPromptHistoryLimitPerProject",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: PromptDefaults.UserPromptHistoryLimitPerProject);

            migrationBuilder.AddColumn<int>(
                name: "UserPromptMaxLengthChars",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: PromptDefaults.UserPromptMaxLengthChars);

            migrationBuilder.CreateTable(
                name: "ProjectPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptType = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPrompts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPrompts_ProjectId_PromptType_ContentHash",
                table: "ProjectPrompts",
                columns: new[] { "ProjectId", "PromptType", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPrompts_ProjectId_PromptType_LastUsedAt",
                table: "ProjectPrompts",
                columns: new[] { "ProjectId", "PromptType", "LastUsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectPrompts");

            migrationBuilder.DropColumn(
                name: "DefaultSystemPrompt",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "SystemPromptHistoryLimitPerProject",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "SystemPromptMaxLengthChars",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "UserPromptHistoryLimitPerProject",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "UserPromptMaxLengthChars",
                table: "PlatformSettings");
        }
    }
}
