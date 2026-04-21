using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSettingsMaxFileSizeBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MaxFileSizeBytes",
                table: "PlatformSettings",
                type: "bigint",
                nullable: false,
                defaultValue: 26214400L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxFileSizeBytes",
                table: "PlatformSettings");
        }
    }
}
