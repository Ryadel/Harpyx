using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersonalTenantProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPersonal",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill legacy personal tenants created before this column existed.
            migrationBuilder.Sql(
                """
                UPDATE t
                SET t.[IsPersonal] = 1
                FROM [Tenants] t
                WHERE LOWER(t.[Name]) = 'personal'
                  AND EXISTS (
                    SELECT 1
                    FROM [UserTenants] ut
                    WHERE ut.[TenantId] = t.[Id]
                    GROUP BY ut.[TenantId]
                    HAVING COUNT(*) = 1
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPersonal",
                table: "Tenants");
        }
    }
}
