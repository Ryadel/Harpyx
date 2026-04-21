using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations;

[DbContext(typeof(HarpyxDbContext))]
[Migration("20260212173000_UserLlmProviderMetadata")]
public class UserLlmProviderMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "UserLlmProviders",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "UserLlmProviders",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "UserLlmProviders",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Description",
            table: "UserLlmProviders");

        migrationBuilder.DropColumn(
            name: "Name",
            table: "UserLlmProviders");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "UserLlmProviders");
    }
}
