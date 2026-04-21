using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSettingsContainerExtractionLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContainerMaxFilesPerRoot",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<int>(
                name: "ContainerMaxNestingDepth",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<long>(
                name: "ContainerMaxSingleEntrySizeBytes",
                table: "PlatformSettings",
                type: "bigint",
                nullable: false,
                defaultValue: 104857600L);

            migrationBuilder.AddColumn<long>(
                name: "ContainerMaxTotalExtractedBytesPerRoot",
                table: "PlatformSettings",
                type: "bigint",
                nullable: false,
                defaultValue: 524288000L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContainerMaxFilesPerRoot",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ContainerMaxNestingDepth",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ContainerMaxSingleEntrySizeBytes",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ContainerMaxTotalExtractedBytesPerRoot",
                table: "PlatformSettings");
        }
    }
}
