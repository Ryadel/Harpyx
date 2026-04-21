using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentLineageAndFormatExtractionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents");

            migrationBuilder.AddColumn<string>(
                name: "ContainerPath",
                table: "Documents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContainerType",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtractionState",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsContainer",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NestingLevel",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginatingUploadId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentDocumentId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootContainerDocumentId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ParentDocumentId",
                table: "Documents",
                column: "ParentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_RootContainerDocumentId_NestingLevel",
                table: "Documents",
                columns: new[] { "ProjectId", "RootContainerDocumentId", "NestingLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RootContainerDocumentId",
                table: "Documents",
                column: "RootContainerDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Documents_ParentDocumentId",
                table: "Documents",
                column: "ParentDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Documents_RootContainerDocumentId",
                table: "Documents",
                column: "RootContainerDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Documents_ParentDocumentId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Documents_RootContainerDocumentId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ParentDocumentId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_RootContainerDocumentId_NestingLevel",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_RootContainerDocumentId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ContainerPath",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ContainerType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExtractionState",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsContainer",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NestingLevel",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OriginatingUploadId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ParentDocumentId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RootContainerDocumentId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents",
                column: "ProjectId");
        }
    }
}
