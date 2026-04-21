using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserLlmProviderOcrCapability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultOcr",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OcrModel",
                table: "UserLlmProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsOcr",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultOcr",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "OcrModel",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "SupportsOcr",
                table: "UserLlmProviders");
        }
    }
}
