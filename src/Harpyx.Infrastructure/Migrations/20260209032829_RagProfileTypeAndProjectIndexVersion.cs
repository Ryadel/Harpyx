using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RagProfileTypeAndProjectIndexVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLlmProviders_UserId_Provider",
                table: "UserLlmProviders");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentId_ChunkIndex",
                table: "DocumentChunks");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "UserLlmProviders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "RagEmbeddingProviderId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RagIndexVersion",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "IndexVersion",
                table: "DocumentChunks",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmProviders_UserId_Type_Provider",
                table: "UserLlmProviders",
                columns: new[] { "UserId", "Type", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_RagEmbeddingProviderId",
                table: "Projects",
                column: "RagEmbeddingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_ChunkIndex_IndexVersion",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "ChunkIndex", "IndexVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserLlmProviders_RagEmbeddingProviderId",
                table: "Projects",
                column: "RagEmbeddingProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserLlmProviders_RagEmbeddingProviderId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_UserLlmProviders_UserId_Type_Provider",
                table: "UserLlmProviders");

            migrationBuilder.DropIndex(
                name: "IX_Projects_RagEmbeddingProviderId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentId_ChunkIndex_IndexVersion",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "RagEmbeddingProviderId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RagIndexVersion",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IndexVersion",
                table: "DocumentChunks");

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmProviders_UserId_Provider",
                table: "UserLlmProviders",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_ChunkIndex",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "ChunkIndex" },
                unique: true);
        }
    }
}
