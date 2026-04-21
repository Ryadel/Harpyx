using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UrlDocumentsEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Documents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UrlDocumentsEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Documents");
        }
    }
}
