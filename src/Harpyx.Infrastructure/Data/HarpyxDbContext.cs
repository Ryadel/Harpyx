using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Harpyx.Application.Defaults;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Data;

public class HarpyxDbContext : DbContext
{
    public HarpyxDbContext(DbContextOptions<HarpyxDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
    public DbSet<ProjectPrompt> ProjectPrompts => Set<ProjectPrompt>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<PlatformUsageLimits> PlatformUsageLimits => Set<PlatformUsageLimits>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<LlmConnection> LlmConnections => Set<LlmConnection>();
    public DbSet<LlmModel> LlmModels => Set<LlmModel>();
    public DbSet<UserLlmModelPreference> UserLlmModelPreferences => Set<UserLlmModelPreference>();
    public DbSet<UserApiKey> UserApiKeys => Set<UserApiKey>();
    public DbSet<ProjectChatMessage> ProjectChatMessages => Set<ProjectChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.IsActive).IsRequired();
            entity.Property(t => t.IsPersonal).IsRequired().HasDefaultValue(false);
            entity.HasIndex(t => t.CreatedByUserId);
            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Name).HasMaxLength(200).IsRequired();
            entity.Property(w => w.Description).HasMaxLength(1000);
            entity.Property(w => w.IsActive).IsRequired();
            entity.Property(w => w.IsChatLlmEnabled).IsRequired();
            entity.Property(w => w.IsRagLlmEnabled).IsRequired();
            entity.Property(w => w.IsOcrLlmEnabled).IsRequired();
            entity.HasIndex(w => new { w.TenantId, w.Name }).IsUnique();
            entity.HasIndex(w => w.CreatedByUserId);
            entity.HasOne(w => w.Tenant)
                .WithMany(t => t.Workspaces)
                .HasForeignKey(w => w.TenantId);
            entity.HasOne(w => w.CreatedByUser)
                .WithMany()
                .HasForeignKey(w => w.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(w => w.ChatModel)
                .WithMany(lp => lp.ChatWorkspaces)
                .HasForeignKey(w => w.ChatModelId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(w => w.RagEmbeddingModel)
                .WithMany(lp => lp.RagWorkspaces)
                .HasForeignKey(w => w.RagEmbeddingModelId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(w => w.OcrModel)
                .WithMany(lp => lp.OcrWorkspaces)
                .HasForeignKey(w => w.OcrModelId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.RagIndexVersion).IsRequired();
            entity.Property(p => p.ChatLlmOverride).IsRequired();
            entity.Property(p => p.RagLlmOverride).IsRequired();
            entity.Property(p => p.OcrLlmOverride).IsRequired();
            entity.Property(p => p.AutoExtendLifetimeOnActivity).IsRequired().HasDefaultValue(true);
            entity.HasIndex(p => p.CreatedByUserId);
            entity.HasIndex(p => p.LifetimeExpiresAtUtc);
            entity.HasOne(p => p.Workspace)
                .WithMany(w => w.Projects)
                .HasForeignKey(p => p.WorkspaceId);
            entity.HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(p => p.ChatModel)
                .WithMany(lp => lp.ChatProjects)
                .HasForeignKey(p => p.ChatModelId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(p => p.RagEmbeddingModel)
                .WithMany(lp => lp.RagProjects)
                .HasForeignKey(p => p.RagEmbeddingModelId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(p => p.OcrModel)
                .WithMany(lp => lp.OcrProjects)
                .HasForeignKey(p => p.OcrModelId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ProjectPrompt>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.PromptType).IsRequired();
            entity.Property(p => p.Content).IsRequired();
            entity.Property(p => p.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(p => p.IsFavorite).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.LastUsedAt).IsRequired();
            entity.HasIndex(p => new { p.ProjectId, p.PromptType, p.LastUsedAt });
            entity.HasIndex(p => new { p.ProjectId, p.PromptType, p.ContentHash }).IsUnique();
            entity.HasOne(p => p.Project)
                .WithMany(project => project.Prompts)
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).HasMaxLength(20).IsRequired();
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.MessageTimestamp).IsRequired();
            entity.HasIndex(m => new { m.ProjectId, m.MessageTimestamp });
            entity.HasOne(m => m.Project)
                .WithMany(p => p.ChatMessages)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FileName).HasMaxLength(512).IsRequired();
            entity.Property(d => d.ContentType).HasMaxLength(200).IsRequired();
            entity.Property(d => d.StorageKey).HasMaxLength(512).IsRequired();
            entity.Property(d => d.SourceUrl).HasMaxLength(2048);
            entity.Property(d => d.ContainerPath).HasMaxLength(2048);
            entity.Property(d => d.NestingLevel).IsRequired().HasDefaultValue(0);
            entity.Property(d => d.IsContainer).IsRequired().HasDefaultValue(false);
            entity.Property(d => d.ContainerType).IsRequired().HasDefaultValue(DocumentContainerType.None);
            entity.Property(d => d.ExtractionState).IsRequired().HasDefaultValue(DocumentExtractionState.Pending);
            entity.HasIndex(d => d.UploadedByUserId);
            entity.HasIndex(d => d.ParentDocumentId);
            entity.HasIndex(d => d.RootContainerDocumentId);
            entity.HasIndex(d => new { d.ProjectId, d.RootContainerDocumentId, d.NestingLevel });
            entity.HasOne(d => d.Project)
                .WithMany(p => p.Documents)
                .HasForeignKey(d => d.ProjectId);
            entity.HasOne(d => d.UploadedByUser)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.ParentDocument)
                .WithMany(d => d.ChildDocuments)
                .HasForeignKey(d => d.ParentDocumentId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.RootContainerDocument)
                .WithMany()
                .HasForeignKey(d => d.RootContainerDocumentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.SourceType).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Content).IsRequired();
            entity.Property(c => c.Embedding).IsRequired();
            entity.HasIndex(c => new { c.DocumentId, c.ChunkIndex, c.IndexVersion }).IsUnique();
            entity.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId);
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.JobType).HasMaxLength(200).IsRequired();
            entity.HasOne(j => j.Document)
                .WithMany(d => d.Jobs)
                .HasForeignKey(j => j.DocumentId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.ObjectId).HasMaxLength(200);
            entity.Property(u => u.SubjectId).HasMaxLength(200);
            entity.Property(u => u.Email).HasMaxLength(320).IsRequired();
            entity.Property(u => u.LastLoginProvider).HasMaxLength(100);
            entity.Property(u => u.LastLoginAt);
        });

        modelBuilder.Entity<PlatformSettings>(entity =>
        {
            entity.HasKey(ps => ps.Id);
            entity.Property(ps => ps.DefaultSystemPrompt).IsRequired().HasDefaultValue(PromptDefaults.DefaultSystemPrompt);
            entity.Property(ps => ps.SystemPromptMaxLengthChars).IsRequired().HasDefaultValue(PromptDefaults.SystemPromptMaxLengthChars);
            entity.Property(ps => ps.UserPromptMaxLengthChars).IsRequired().HasDefaultValue(PromptDefaults.UserPromptMaxLengthChars);
            entity.Property(ps => ps.SystemPromptHistoryLimitPerProject).IsRequired().HasDefaultValue(PromptDefaults.SystemPromptHistoryLimitPerProject);
            entity.Property(ps => ps.UserPromptHistoryLimitPerProject).IsRequired().HasDefaultValue(PromptDefaults.UserPromptHistoryLimitPerProject);
            entity.Property(ps => ps.UserSelfRegistrationEnabled).IsRequired();
            entity.Property(ps => ps.QuarantineEnabled).IsRequired().HasDefaultValue(true);
            entity.Property(ps => ps.UrlDocumentsEnabled).IsRequired().HasDefaultValue(true);
            entity.Property(ps => ps.MaxFileSizeBytes).IsRequired().HasDefaultValue(25L * 1024L * 1024L);
            entity.Property(ps => ps.ContainerMaxNestingDepth).IsRequired().HasDefaultValue(3);
            entity.Property(ps => ps.ContainerMaxTotalExtractedBytesPerRoot).IsRequired().HasDefaultValue(500L * 1024L * 1024L);
            entity.Property(ps => ps.ContainerMaxFilesPerRoot).IsRequired().HasDefaultValue(200);
            entity.Property(ps => ps.ContainerMaxSingleEntrySizeBytes).IsRequired().HasDefaultValue(100L * 1024L * 1024L);
            entity.Property(ps => ps.RagTopK).IsRequired().HasDefaultValue(6);
            entity.Property(ps => ps.RagMaxContextChars).IsRequired().HasDefaultValue(10000);
            entity.Property(ps => ps.RagRrfK).IsRequired().HasDefaultValue(60);
            entity.Property(ps => ps.RagLexicalCandidateK).IsRequired().HasDefaultValue(24);
            entity.Property(ps => ps.RagVectorCandidateK).IsRequired().HasDefaultValue(24);
            entity.Property(ps => ps.RagKeywordMaxCount).IsRequired().HasDefaultValue(12);
            entity.Property(ps => ps.RagUseRakeKeywordExtraction).IsRequired().HasDefaultValue(true);
            entity.Property(ps => ps.RagContextCacheTtlSeconds).IsRequired().HasDefaultValue(300);
            entity.Property(ps => ps.RagUseOpenSearchIndexing).IsRequired().HasDefaultValue(true);
            entity.Property(ps => ps.RagUseOpenSearchRetrieval).IsRequired().HasDefaultValue(true);
            entity.Property(ps => ps.RagFallbackToSqlRetrievalOnOpenSearchFailure).IsRequired().HasDefaultValue(true);
            entity.Property(ps => ps.ChatHistoryLimitPerProject).IsRequired().HasDefaultValue(PromptDefaults.ChatHistoryLimitPerProject);
        });

        modelBuilder.Entity<PlatformUsageLimits>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.EnableOcr).IsRequired().HasDefaultValue(true);
            entity.Property(l => l.EnableRagIndexing).IsRequired().HasDefaultValue(true);
            entity.Property(l => l.EnableApi).IsRequired().HasDefaultValue(true);
        });

        modelBuilder.Entity<UserInvitation>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Email).HasMaxLength(320).IsRequired();
            entity.Property(i => i.Scope).IsRequired();
            entity.Property(i => i.Status).IsRequired();
            entity.Property(i => i.ExpiresAt).IsRequired();
            entity.HasIndex(i => new { i.Email, i.Status, i.ExpiresAt });
            entity.HasOne(i => i.Tenant)
                .WithMany(t => t.Invitations)
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(i => i.InvitedByUser)
                .WithMany(u => u.SentInvitations)
                .HasForeignKey(i => i.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.AcceptedUser)
                .WithMany(u => u.AcceptedInvitations)
                .HasForeignKey(i => i.AcceptedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserTenant>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.TenantId });
            entity.Property(x => x.TenantRole).IsRequired();
            entity.Property(x => x.CanGrant).IsRequired().HasDefaultValue(false);
            entity.Property(x => x.GrantedAt);
            entity.HasIndex(x => x.GrantedByUserId);

            entity.HasOne(x => x.User)
                .WithMany(u => u.TenantAssignments)
                .HasForeignKey(x => x.UserId);

            entity.HasOne(x => x.Tenant)
                .WithMany(t => t.UserAssignments)
                .HasForeignKey(x => x.TenantId);

            entity.HasOne(x => x.GrantedByUser)
                .WithMany(u => u.GrantedTenantMemberships)
                .HasForeignKey(x => x.GrantedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.EventType).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<LlmConnection>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.Scope, c.UserId, c.Provider });
            entity.Property(c => c.Scope).IsRequired();
            entity.Property(c => c.Provider).IsRequired();
            entity.Property(c => c.Name).HasMaxLength(200);
            entity.Property(c => c.Description).HasMaxLength(1000);
            entity.Property(c => c.Notes).HasMaxLength(4000);
            entity.Property(c => c.BaseUrl).HasMaxLength(1000);
            entity.Property(c => c.EncryptedApiKey).HasMaxLength(2048);
            entity.Property(c => c.ApiKeyLast4).HasMaxLength(4);
            entity.Property(c => c.IsEnabled).IsRequired().HasDefaultValue(true);
            // Scope values are int-backed: Personal=0, Hosted=1 (see LlmConnectionScope).
            // Hosted connections are admin-managed and must not reference a user;
            // Personal connections belong to a specific user.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_LlmConnections_Scope_UserId",
                "([Scope] = 1 AND [UserId] IS NULL) OR ([Scope] = 0 AND [UserId] IS NOT NULL)"));
            entity.HasOne(c => c.User)
                .WithMany(u => u.LlmConnections)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LlmModel>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.ConnectionId, m.Capability, m.ModelId }).IsUnique();
            entity.Property(m => m.Capability).IsRequired();
            entity.Property(m => m.ModelId).HasMaxLength(200).IsRequired();
            entity.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(m => m.IsPublished).IsRequired().HasDefaultValue(true);
            entity.Property(m => m.IsEnabled).IsRequired().HasDefaultValue(true);
            entity.HasOne(m => m.Connection)
                .WithMany(c => c.Models)
                .HasForeignKey(m => m.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserLlmModelPreference>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.UserId, p.Usage }).IsUnique();
            entity.Property(p => p.Usage).IsRequired();
            entity.HasOne(p => p.User)
                .WithMany(u => u.LlmModelPreferences)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(p => p.LlmModel)
                .WithMany(m => m.UserPreferences)
                .HasForeignKey(p => p.LlmModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserApiKey>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Name).HasMaxLength(200).IsRequired();
            entity.Property(k => k.KeyId).HasMaxLength(64).IsRequired();
            entity.Property(k => k.KeyHash).HasMaxLength(256).IsRequired();
            entity.Property(k => k.KeySalt).HasMaxLength(256).IsRequired();
            entity.Property(k => k.KeyHashIterations).IsRequired();
            entity.Property(k => k.KeyPreview).HasMaxLength(16).IsRequired();
            entity.Property(k => k.KeyLast4).HasMaxLength(4).IsRequired();
            entity.Property(k => k.Permissions).IsRequired();
            entity.Property(k => k.IsActive).IsRequired().HasDefaultValue(true);
            entity.HasIndex(k => k.KeyId).IsUnique();
            entity.HasIndex(k => new { k.UserId, k.IsActive });
            entity.HasOne(k => k.User)
                .WithMany(u => u.ApiKeys)
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
