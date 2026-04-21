using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSettingsUserSelfRegistrationEnabledRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AllowUserSelfRegistration",
                table: "PlatformSettings",
                newName: "UserSelfRegistrationEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserSelfRegistrationEnabled",
                table: "PlatformSettings",
                newName: "AllowUserSelfRegistration");
        }
    }
}
