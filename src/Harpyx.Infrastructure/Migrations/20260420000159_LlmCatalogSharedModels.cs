using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harpyx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LlmCatalogSharedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserLlmProviders_ChatProviderId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserLlmProviders_OcrProviderId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserLlmProviders_RagEmbeddingProviderId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserLlmProviders_ChatProviderId",
                table: "Workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserLlmProviders_OcrProviderId",
                table: "Workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserLlmProviders_RagEmbeddingProviderId",
                table: "Workspaces");

            migrationBuilder.RenameColumn(
                name: "RagEmbeddingProviderId",
                table: "Workspaces",
                newName: "RagEmbeddingModelId");

            migrationBuilder.RenameColumn(
                name: "OcrProviderId",
                table: "Workspaces",
                newName: "OcrModelId");

            migrationBuilder.RenameColumn(
                name: "ChatProviderId",
                table: "Workspaces",
                newName: "ChatModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Workspaces_RagEmbeddingProviderId",
                table: "Workspaces",
                newName: "IX_Workspaces_RagEmbeddingModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Workspaces_OcrProviderId",
                table: "Workspaces",
                newName: "IX_Workspaces_OcrModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Workspaces_ChatProviderId",
                table: "Workspaces",
                newName: "IX_Workspaces_ChatModelId");

            migrationBuilder.RenameColumn(
                name: "RagEmbeddingProviderId",
                table: "Projects",
                newName: "RagEmbeddingModelId");

            migrationBuilder.RenameColumn(
                name: "OcrProviderId",
                table: "Projects",
                newName: "OcrModelId");

            migrationBuilder.RenameColumn(
                name: "ChatProviderId",
                table: "Projects",
                newName: "ChatModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_RagEmbeddingProviderId",
                table: "Projects",
                newName: "IX_Projects_RagEmbeddingModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_OcrProviderId",
                table: "Projects",
                newName: "IX_Projects_OcrModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_ChatProviderId",
                table: "Projects",
                newName: "IX_Projects_ChatModelId");

            migrationBuilder.CreateTable(
                name: "LlmConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BaseUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EncryptedApiKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ApiKeyLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmConnections", x => x.Id);
                    table.CheckConstraint(
                        "CK_LlmConnections_Scope_UserId",
                        "([Scope] = 1 AND [UserId] IS NULL) OR ([Scope] = 0 AND [UserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LlmConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LlmModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capability = table.Column<int>(type: "int", nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "int", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LlmModels_LlmConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "LlmConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLlmModelPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Usage = table.Column<int>(type: "int", nullable: false),
                    LlmModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLlmModelPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLlmModelPreferences_LlmModels_LlmModelId",
                        column: x => x.LlmModelId,
                        principalTable: "LlmModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLlmModelPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmConnections_Scope_UserId_Provider",
                table: "LlmConnections",
                columns: new[] { "Scope", "UserId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmConnections_UserId",
                table: "LlmConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmModels_ConnectionId_Capability_ModelId",
                table: "LlmModels",
                columns: new[] { "ConnectionId", "Capability", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmModelPreferences_LlmModelId",
                table: "UserLlmModelPreferences",
                column: "LlmModelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmModelPreferences_UserId_Usage",
                table: "UserLlmModelPreferences",
                columns: new[] { "UserId", "Usage" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO LlmConnections (
                    Id,
                    Scope,
                    UserId,
                    Provider,
                    Name,
                    Description,
                    Notes,
                    BaseUrl,
                    EncryptedApiKey,
                    ApiKeyLast4,
                    IsEnabled,
                    CreatedAt,
                    UpdatedAt)
                SELECT
                    Id,
                    0,
                    UserId,
                    Provider,
                    Name,
                    Description,
                    Notes,
                    NULL,
                    EncryptedApiKey,
                    ApiKeyLast4,
                    1,
                    CreatedAt,
                    UpdatedAt
                FROM UserLlmProviders;

                INSERT INTO LlmModels (
                    Id,
                    ConnectionId,
                    Capability,
                    ModelId,
                    DisplayName,
                    EmbeddingDimensions,
                    IsPublished,
                    IsEnabled,
                    CreatedAt,
                    UpdatedAt)
                SELECT
                    NEWID(),
                    Id,
                    1,
                    COALESCE(NULLIF(ChatModel, ''), CASE Provider WHEN 2 THEN 'claude-sonnet-4-5-20250929' WHEN 3 THEN 'gemini-1.5-pro' ELSE 'gpt-4o' END),
                    CONCAT(COALESCE(NULLIF(Name, ''), CASE Provider WHEN 1 THEN 'OpenAI' WHEN 2 THEN 'Claude' WHEN 3 THEN 'Google' ELSE 'Provider' END), ' - ', COALESCE(NULLIF(ChatModel, ''), CASE Provider WHEN 2 THEN 'claude-sonnet-4-5-20250929' WHEN 3 THEN 'gemini-1.5-pro' ELSE 'gpt-4o' END)),
                    NULL,
                    1,
                    1,
                    CreatedAt,
                    UpdatedAt
                FROM UserLlmProviders
                WHERE SupportsChat = 1;

                INSERT INTO LlmModels (
                    Id,
                    ConnectionId,
                    Capability,
                    ModelId,
                    DisplayName,
                    EmbeddingDimensions,
                    IsPublished,
                    IsEnabled,
                    CreatedAt,
                    UpdatedAt)
                SELECT
                    NEWID(),
                    Id,
                    2,
                    COALESCE(NULLIF(RagEmbeddingModel, ''), CASE Provider WHEN 3 THEN 'gemini-embedding-001' ELSE 'text-embedding-3-small' END),
                    CONCAT(COALESCE(NULLIF(Name, ''), CASE Provider WHEN 1 THEN 'OpenAI' WHEN 2 THEN 'Claude' WHEN 3 THEN 'Google' ELSE 'Provider' END), ' - ', COALESCE(NULLIF(RagEmbeddingModel, ''), CASE Provider WHEN 3 THEN 'gemini-embedding-001' ELSE 'text-embedding-3-small' END)),
                    CASE Provider WHEN 3 THEN 768 ELSE 1536 END,
                    1,
                    1,
                    CreatedAt,
                    UpdatedAt
                FROM UserLlmProviders
                WHERE SupportsRagEmbedding = 1;

                INSERT INTO LlmModels (
                    Id,
                    ConnectionId,
                    Capability,
                    ModelId,
                    DisplayName,
                    EmbeddingDimensions,
                    IsPublished,
                    IsEnabled,
                    CreatedAt,
                    UpdatedAt)
                SELECT
                    NEWID(),
                    Id,
                    3,
                    COALESCE(NULLIF(OcrModel, ''), CASE Provider WHEN 2 THEN 'claude-sonnet-4-5-20250929' WHEN 3 THEN 'gemini-1.5-pro' ELSE 'gpt-4o' END),
                    CONCAT(COALESCE(NULLIF(Name, ''), CASE Provider WHEN 1 THEN 'OpenAI' WHEN 2 THEN 'Claude' WHEN 3 THEN 'Google' ELSE 'Provider' END), ' - ', COALESCE(NULLIF(OcrModel, ''), CASE Provider WHEN 2 THEN 'claude-sonnet-4-5-20250929' WHEN 3 THEN 'gemini-1.5-pro' ELSE 'gpt-4o' END)),
                    NULL,
                    1,
                    1,
                    CreatedAt,
                    UpdatedAt
                FROM UserLlmProviders
                WHERE SupportsOcr = 1;

                UPDATE p
                SET ChatModelId = m.Id
                FROM Projects p
                INNER JOIN LlmModels m ON m.ConnectionId = p.ChatModelId AND m.Capability = 1;

                UPDATE p
                SET RagEmbeddingModelId = m.Id
                FROM Projects p
                INNER JOIN LlmModels m ON m.ConnectionId = p.RagEmbeddingModelId AND m.Capability = 2;

                UPDATE p
                SET OcrModelId = m.Id
                FROM Projects p
                INNER JOIN LlmModels m ON m.ConnectionId = p.OcrModelId AND m.Capability = 3;

                UPDATE Projects
                SET ChatModelId = NULL
                WHERE ChatModelId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LlmModels m WHERE m.Id = Projects.ChatModelId);

                UPDATE Projects
                SET RagEmbeddingModelId = NULL
                WHERE RagEmbeddingModelId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LlmModels m WHERE m.Id = Projects.RagEmbeddingModelId);

                UPDATE Projects
                SET OcrModelId = NULL
                WHERE OcrModelId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LlmModels m WHERE m.Id = Projects.OcrModelId);

                UPDATE w
                SET ChatModelId = m.Id
                FROM Workspaces w
                INNER JOIN LlmModels m ON m.ConnectionId = w.ChatModelId AND m.Capability = 1;

                UPDATE w
                SET RagEmbeddingModelId = m.Id
                FROM Workspaces w
                INNER JOIN LlmModels m ON m.ConnectionId = w.RagEmbeddingModelId AND m.Capability = 2;

                UPDATE w
                SET OcrModelId = m.Id
                FROM Workspaces w
                INNER JOIN LlmModels m ON m.ConnectionId = w.OcrModelId AND m.Capability = 3;

                UPDATE Workspaces
                SET ChatModelId = NULL
                WHERE ChatModelId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LlmModels m WHERE m.Id = Workspaces.ChatModelId);

                UPDATE Workspaces
                SET RagEmbeddingModelId = NULL
                WHERE RagEmbeddingModelId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LlmModels m WHERE m.Id = Workspaces.RagEmbeddingModelId);

                UPDATE Workspaces
                SET OcrModelId = NULL
                WHERE OcrModelId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LlmModels m WHERE m.Id = Workspaces.OcrModelId);

                WITH ChatDefaults AS (
                    SELECT p.UserId, m.Id AS LlmModelId,
                           ROW_NUMBER() OVER (PARTITION BY p.UserId ORDER BY COALESCE(p.UpdatedAt, p.CreatedAt) DESC) AS rn
                    FROM UserLlmProviders p
                    INNER JOIN LlmModels m ON m.ConnectionId = p.Id AND m.Capability = 1
                    WHERE p.IsDefaultChat = 1
                )
                INSERT INTO UserLlmModelPreferences (Id, UserId, Usage, LlmModelId, CreatedAt, UpdatedAt)
                SELECT NEWID(), UserId, 1, LlmModelId, SYSUTCDATETIME(), NULL
                FROM ChatDefaults
                WHERE rn = 1;

                WITH RagDefaults AS (
                    SELECT p.UserId, m.Id AS LlmModelId,
                           ROW_NUMBER() OVER (PARTITION BY p.UserId ORDER BY COALESCE(p.UpdatedAt, p.CreatedAt) DESC) AS rn
                    FROM UserLlmProviders p
                    INNER JOIN LlmModels m ON m.ConnectionId = p.Id AND m.Capability = 2
                    WHERE p.IsDefaultRagEmbedding = 1
                )
                INSERT INTO UserLlmModelPreferences (Id, UserId, Usage, LlmModelId, CreatedAt, UpdatedAt)
                SELECT NEWID(), UserId, 2, LlmModelId, SYSUTCDATETIME(), NULL
                FROM RagDefaults
                WHERE rn = 1;

                WITH OcrDefaults AS (
                    SELECT p.UserId, m.Id AS LlmModelId,
                           ROW_NUMBER() OVER (PARTITION BY p.UserId ORDER BY COALESCE(p.UpdatedAt, p.CreatedAt) DESC) AS rn
                    FROM UserLlmProviders p
                    INNER JOIN LlmModels m ON m.ConnectionId = p.Id AND m.Capability = 3
                    WHERE p.IsDefaultOcr = 1
                )
                INSERT INTO UserLlmModelPreferences (Id, UserId, Usage, LlmModelId, CreatedAt, UpdatedAt)
                SELECT NEWID(), UserId, 3, LlmModelId, SYSUTCDATETIME(), NULL
                FROM OcrDefaults
                WHERE rn = 1;
                """);

            migrationBuilder.DropTable(
                name: "UserLlmProviders");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_LlmModels_ChatModelId",
                table: "Projects",
                column: "ChatModelId",
                principalTable: "LlmModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_LlmModels_OcrModelId",
                table: "Projects",
                column: "OcrModelId",
                principalTable: "LlmModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_LlmModels_RagEmbeddingModelId",
                table: "Projects",
                column: "RagEmbeddingModelId",
                principalTable: "LlmModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_LlmModels_ChatModelId",
                table: "Workspaces",
                column: "ChatModelId",
                principalTable: "LlmModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_LlmModels_OcrModelId",
                table: "Workspaces",
                column: "OcrModelId",
                principalTable: "LlmModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_LlmModels_RagEmbeddingModelId",
                table: "Workspaces",
                column: "RagEmbeddingModelId",
                principalTable: "LlmModels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_LlmModels_ChatModelId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_LlmModels_OcrModelId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_LlmModels_RagEmbeddingModelId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_LlmModels_ChatModelId",
                table: "Workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_LlmModels_OcrModelId",
                table: "Workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_LlmModels_RagEmbeddingModelId",
                table: "Workspaces");

            migrationBuilder.DropTable(
                name: "UserLlmModelPreferences");

            migrationBuilder.DropTable(
                name: "LlmModels");

            migrationBuilder.DropTable(
                name: "LlmConnections");

            migrationBuilder.RenameColumn(
                name: "RagEmbeddingModelId",
                table: "Workspaces",
                newName: "RagEmbeddingProviderId");

            migrationBuilder.RenameColumn(
                name: "OcrModelId",
                table: "Workspaces",
                newName: "OcrProviderId");

            migrationBuilder.RenameColumn(
                name: "ChatModelId",
                table: "Workspaces",
                newName: "ChatProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_Workspaces_RagEmbeddingModelId",
                table: "Workspaces",
                newName: "IX_Workspaces_RagEmbeddingProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_Workspaces_OcrModelId",
                table: "Workspaces",
                newName: "IX_Workspaces_OcrProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_Workspaces_ChatModelId",
                table: "Workspaces",
                newName: "IX_Workspaces_ChatProviderId");

            migrationBuilder.RenameColumn(
                name: "RagEmbeddingModelId",
                table: "Projects",
                newName: "RagEmbeddingProviderId");

            migrationBuilder.RenameColumn(
                name: "OcrModelId",
                table: "Projects",
                newName: "OcrProviderId");

            migrationBuilder.RenameColumn(
                name: "ChatModelId",
                table: "Projects",
                newName: "ChatProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_RagEmbeddingModelId",
                table: "Projects",
                newName: "IX_Projects_RagEmbeddingProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_OcrModelId",
                table: "Projects",
                newName: "IX_Projects_OcrProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_ChatModelId",
                table: "Projects",
                newName: "IX_Projects_ChatProviderId");

            migrationBuilder.CreateTable(
                name: "UserLlmProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiKeyLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ChatModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EncryptedApiKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsDefaultChat = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultOcr = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultRagEmbedding = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OcrModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    RagEmbeddingModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupportsChat = table.Column<bool>(type: "bit", nullable: false),
                    SupportsOcr = table.Column<bool>(type: "bit", nullable: false),
                    SupportsRagEmbedding = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLlmProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLlmProviders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmProviders_UserId_Provider",
                table: "UserLlmProviders",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserLlmProviders_ChatProviderId",
                table: "Projects",
                column: "ChatProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserLlmProviders_OcrProviderId",
                table: "Projects",
                column: "OcrProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserLlmProviders_RagEmbeddingProviderId",
                table: "Projects",
                column: "RagEmbeddingProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserLlmProviders_ChatProviderId",
                table: "Workspaces",
                column: "ChatProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserLlmProviders_OcrProviderId",
                table: "Workspaces",
                column: "OcrProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserLlmProviders_RagEmbeddingProviderId",
                table: "Workspaces",
                column: "RagEmbeddingProviderId",
                principalTable: "UserLlmProviders",
                principalColumn: "Id");
        }
    }
}
