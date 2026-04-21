using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserLlmProviderCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Normalize legacy naming (Profiles -> Providers) before applying capability changes.
                IF OBJECT_ID(N'dbo.UserLlmProviders', N'U') IS NULL
                    AND OBJECT_ID(N'dbo.UserLlmProfiles', N'U') IS NOT NULL
                BEGIN
                    EXEC sp_rename N'dbo.UserLlmProfiles', N'UserLlmProviders';
                END;

                IF COL_LENGTH('dbo.Projects', 'RagEmbeddingProviderId') IS NULL
                    AND COL_LENGTH('dbo.Projects', 'RagEmbeddingProfileId') IS NOT NULL
                BEGIN
                    EXEC sp_rename N'dbo.Projects.RagEmbeddingProfileId', N'RagEmbeddingProviderId', 'COLUMN';
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Projects_RagEmbeddingProfileId'
                        AND object_id = OBJECT_ID(N'dbo.Projects')
                ) AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Projects_RagEmbeddingProviderId'
                        AND object_id = OBJECT_ID(N'dbo.Projects')
                )
                BEGIN
                    EXEC sp_rename N'dbo.Projects.IX_Projects_RagEmbeddingProfileId', N'IX_Projects_RagEmbeddingProviderId', N'INDEX';
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProfiles_UserId_Type_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                ) AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProviders_UserId_Type_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                )
                BEGIN
                    EXEC sp_rename N'dbo.UserLlmProviders.IX_UserLlmProfiles_UserId_Type_Provider', N'IX_UserLlmProviders_UserId_Type_Provider', N'INDEX';
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProfiles_UserId_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                ) AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProviders_UserId_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                )
                BEGIN
                    EXEC sp_rename N'dbo.UserLlmProviders.IX_UserLlmProfiles_UserId_Provider', N'IX_UserLlmProviders_UserId_Provider', N'INDEX';
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProviders_UserId_Type_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                )
                BEGIN
                    DROP INDEX [IX_UserLlmProviders_UserId_Type_Provider] ON [dbo].[UserLlmProviders];
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProfiles_UserId_Type_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                )
                BEGIN
                    DROP INDEX [IX_UserLlmProfiles_UserId_Type_Provider] ON [dbo].[UserLlmProviders];
                END;
                """);

            migrationBuilder.AddColumn<string>(
                name: "ChatModel",
                table: "UserLlmProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultChat",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultRagEmbedding",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsChat",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RagEmbeddingModel",
                table: "UserLlmProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsRagEmbedding",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE p
                SET
                    p.SupportsChat = CASE WHEN p.[Type] = 1 THEN 1 ELSE 0 END,
                    p.SupportsRagEmbedding = CASE WHEN p.[Type] = 2 THEN 1 ELSE 0 END,
                    p.ChatModel = CASE WHEN p.[Type] = 1 THEN p.DefaultModel ELSE NULL END,
                    p.RagEmbeddingModel = CASE WHEN p.[Type] = 2 THEN p.DefaultModel ELSE NULL END,
                    p.IsDefaultChat = CASE WHEN p.[Type] = 1 AND p.IsDefault = 1 THEN 1 ELSE 0 END,
                    p.IsDefaultRagEmbedding = CASE WHEN p.[Type] = 2 AND p.IsDefault = 1 THEN 1 ELSE 0 END
                FROM UserLlmProviders p;
                """);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        p.Id,
                        p.UserId,
                        p.Provider,
                        p.[Type],
                        ROW_NUMBER() OVER (
                            PARTITION BY p.UserId, p.Provider
                            ORDER BY
                                CASE WHEN EXISTS (
                                    SELECT 1
                                    FROM Projects pr
                                    WHERE pr.RagEmbeddingProviderId = p.Id
                                ) THEN 0 ELSE 1 END,
                                CASE WHEN p.[Type] = 2 THEN 0 ELSE 1 END,
                                p.CreatedAt,
                                p.Id
                        ) AS rn
                    FROM UserLlmProviders p
                )
                UPDATE keepRow
                SET
                    keepRow.SupportsChat = agg.HasChat,
                    keepRow.SupportsRagEmbedding = agg.HasRag,
                    keepRow.ChatModel = COALESCE(agg.ChatModel, keepRow.ChatModel),
                    keepRow.RagEmbeddingModel = COALESCE(agg.RagModel, keepRow.RagEmbeddingModel),
                    keepRow.IsDefaultChat = agg.HasDefaultChat,
                    keepRow.IsDefaultRagEmbedding = agg.HasDefaultRag
                FROM UserLlmProviders keepRow
                INNER JOIN ranked r ON r.Id = keepRow.Id AND r.rn = 1
                CROSS APPLY (
                    SELECT
                        CAST(MAX(CASE WHEN p2.[Type] = 1 THEN 1 ELSE 0 END) AS bit) AS HasChat,
                        CAST(MAX(CASE WHEN p2.[Type] = 2 THEN 1 ELSE 0 END) AS bit) AS HasRag,
                        CAST(MAX(CASE WHEN p2.[Type] = 1 AND p2.IsDefault = 1 THEN 1 ELSE 0 END) AS bit) AS HasDefaultChat,
                        CAST(MAX(CASE WHEN p2.[Type] = 2 AND p2.IsDefault = 1 THEN 1 ELSE 0 END) AS bit) AS HasDefaultRag,
                        MAX(CASE WHEN p2.[Type] = 1 THEN p2.DefaultModel END) AS ChatModel,
                        MAX(CASE WHEN p2.[Type] = 2 THEN p2.DefaultModel END) AS RagModel
                    FROM UserLlmProviders p2
                    WHERE p2.UserId = keepRow.UserId
                        AND p2.Provider = keepRow.Provider
                ) agg;
                """);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        p.Id,
                        p.UserId,
                        p.Provider,
                        ROW_NUMBER() OVER (
                            PARTITION BY p.UserId, p.Provider
                            ORDER BY
                                CASE WHEN EXISTS (
                                    SELECT 1
                                    FROM Projects pr
                                    WHERE pr.RagEmbeddingProviderId = p.Id
                                ) THEN 0 ELSE 1 END,
                                CASE WHEN p.[Type] = 2 THEN 0 ELSE 1 END,
                                p.CreatedAt,
                                p.Id
                        ) AS rn
                    FROM UserLlmProviders p
                )
                UPDATE pr
                SET pr.RagEmbeddingProviderId = keepRow.Id
                FROM Projects pr
                INNER JOIN ranked dropRow ON dropRow.Id = pr.RagEmbeddingProviderId AND dropRow.rn > 1
                INNER JOIN ranked keepRank
                    ON keepRank.UserId = dropRow.UserId
                    AND keepRank.Provider = dropRow.Provider
                    AND keepRank.rn = 1
                INNER JOIN UserLlmProviders keepRow ON keepRow.Id = keepRank.Id;
                """);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        p.Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY p.UserId, p.Provider
                            ORDER BY
                                CASE WHEN EXISTS (
                                    SELECT 1
                                    FROM Projects pr
                                    WHERE pr.RagEmbeddingProviderId = p.Id
                                ) THEN 0 ELSE 1 END,
                                CASE WHEN p.[Type] = 2 THEN 0 ELSE 1 END,
                                p.CreatedAt,
                                p.Id
                        ) AS rn
                    FROM UserLlmProviders p
                )
                DELETE p
                FROM UserLlmProviders p
                INNER JOIN ranked r ON r.Id = p.Id
                WHERE r.rn > 1;
                """);

            migrationBuilder.DropColumn(
                name: "Type",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "DefaultModel",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "UserLlmProviders");

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_UserLlmProviders_UserId_Provider'
                        AND object_id = OBJECT_ID(N'dbo.UserLlmProviders')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_UserLlmProviders_UserId_Provider]
                    ON [dbo].[UserLlmProviders]([UserId], [Provider]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLlmProviders_UserId_Provider",
                table: "UserLlmProviders");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "UserLlmProviders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefaultModel",
                table: "UserLlmProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "UserLlmProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                INSERT INTO UserLlmProviders
                (
                    Id,
                    CreatedAt,
                    UpdatedAt,
                    UserId,
                    Provider,
                    EncryptedApiKey,
                    ApiKeyLast4,
                    [Type],
                    DefaultModel,
                    IsDefault,
                    SupportsChat,
                    SupportsRagEmbedding,
                    ChatModel,
                    RagEmbeddingModel,
                    IsDefaultChat,
                    IsDefaultRagEmbedding
                )
                SELECT
                    NEWID(),
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.UserId,
                    p.Provider,
                    p.EncryptedApiKey,
                    p.ApiKeyLast4,
                    2,
                    p.RagEmbeddingModel,
                    p.IsDefaultRagEmbedding,
                    p.SupportsChat,
                    p.SupportsRagEmbedding,
                    p.ChatModel,
                    p.RagEmbeddingModel,
                    p.IsDefaultChat,
                    p.IsDefaultRagEmbedding
                FROM UserLlmProviders p
                WHERE p.SupportsChat = 1
                    AND p.SupportsRagEmbedding = 1;
                """);

            migrationBuilder.Sql(
                """
                UPDATE p
                SET
                    p.[Type] = CASE WHEN p.SupportsRagEmbedding = 1 AND p.SupportsChat = 0 THEN 2 ELSE 1 END,
                    p.DefaultModel = CASE WHEN p.SupportsRagEmbedding = 1 AND p.SupportsChat = 0
                        THEN p.RagEmbeddingModel
                        ELSE p.ChatModel
                    END,
                    p.IsDefault = CASE WHEN p.SupportsRagEmbedding = 1 AND p.SupportsChat = 0
                        THEN p.IsDefaultRagEmbedding
                        ELSE p.IsDefaultChat
                    END
                FROM UserLlmProviders p;
                """);

            migrationBuilder.DropColumn(
                name: "RagEmbeddingModel",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "SupportsRagEmbedding",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "ChatModel",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "IsDefaultChat",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "IsDefaultRagEmbedding",
                table: "UserLlmProviders");

            migrationBuilder.DropColumn(
                name: "SupportsChat",
                table: "UserLlmProviders");

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmProviders_UserId_Type_Provider",
                table: "UserLlmProviders",
                columns: new[] { "UserId", "Type", "Provider" },
                unique: true);
        }
    }
}
