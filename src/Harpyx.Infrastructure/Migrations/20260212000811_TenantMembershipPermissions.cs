using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantMembershipPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanGrant",
                table: "UserTenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GrantedAt",
                table: "UserTenants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByUserId",
                table: "UserTenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantRole",
                table: "UserTenants",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                -- Backfill legacy memberships preserving existing broad access.
                UPDATE ut
                SET ut.[TenantRole] = 1, -- TenantManager
                    ut.[CanGrant] = 1,
                    ut.[GrantedAt] = SYSUTCDATETIME()
                FROM [UserTenants] ut;

                -- Ensure personal-tenant owners are marked as TenantOwner.
                UPDATE ut
                SET ut.[TenantRole] = 0, -- TenantOwner
                    ut.[CanGrant] = 1,
                    ut.[GrantedByUserId] = COALESCE(t.[CreatedByUserId], ut.[UserId])
                FROM [UserTenants] ut
                INNER JOIN [Tenants] t ON t.[Id] = ut.[TenantId]
                WHERE t.[IsPersonal] = 1
                  AND (
                        t.[CreatedByUserId] = ut.[UserId]
                        OR (
                            t.[CreatedByUserId] IS NULL
                            AND ut.[TenantId] IN (
                                SELECT [TenantId]
                                FROM [UserTenants]
                                GROUP BY [TenantId]
                                HAVING COUNT(*) = 1
                            )
                        )
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_GrantedByUserId",
                table: "UserTenants",
                column: "GrantedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Users_GrantedByUserId",
                table: "UserTenants",
                column: "GrantedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Users_GrantedByUserId",
                table: "UserTenants");

            migrationBuilder.DropIndex(
                name: "IX_UserTenants_GrantedByUserId",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "CanGrant",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "GrantedAt",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "GrantedByUserId",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "TenantRole",
                table: "UserTenants");
        }
    }
}
