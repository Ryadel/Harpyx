using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSettingsRagRuntimeTuning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RagContextCacheTtlSeconds",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<bool>(
                name: "RagFallbackToSqlRetrievalOnOpenSearchFailure",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "RagKeywordMaxCount",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<int>(
                name: "RagLexicalCandidateK",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "RagMaxContextChars",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 10000);

            migrationBuilder.AddColumn<int>(
                name: "RagRrfK",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "RagTopK",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<bool>(
                name: "RagUseOpenSearchIndexing",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RagUseOpenSearchRetrieval",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "RagVectorCandidateK",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 24);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RagContextCacheTtlSeconds",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagFallbackToSqlRetrievalOnOpenSearchFailure",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagKeywordMaxCount",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagLexicalCandidateK",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagMaxContextChars",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagRrfK",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagTopK",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagUseOpenSearchIndexing",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagUseOpenSearchRetrieval",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RagVectorCandidateK",
                table: "PlatformSettings");
        }
    }
}
