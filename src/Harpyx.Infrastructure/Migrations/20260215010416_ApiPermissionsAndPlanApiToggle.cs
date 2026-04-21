using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApiPermissionsAndPlanApiToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Permissions",
                table: "UserApiKeys",
                type: "int",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<bool>(
                name: "EnableApi",
                table: "PlanLimits",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "UserApiKeys");

            migrationBuilder.DropColumn(
                name: "EnableApi",
                table: "PlanLimits");
        }
    }
}
