using FluentAssertions;
using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Application.Services;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Moq;
using Xunit;

namespace Harpyx.WebApp.UnitTests;

public class DocumentServiceTests
{
    [Fact]
    public async Task UploadAsync_WhenArchiveFile_SetsContainerMetadata()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(true, "application/zip"));
        malwareScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanVerdict.Clean));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Document? saved = null;
        documents.Setup(d => d.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);
        var result = await service.UploadAsync(
            new UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "bundle.zip", "application/zip", content, content.Length),
            CancellationToken.None);

        result.State.Should().Be(DocumentState.Queued);
        saved.Should().NotBeNull();
        saved!.IsContainer.Should().BeTrue();
        saved.ContainerType.Should().Be(DocumentContainerType.Archive);
        saved.ExtractionState.Should().Be(DocumentExtractionState.Pending);
    }

    [Fact]
    public async Task UploadAsync_WhenMsgFile_SetsEmailContainerMetadata()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(true, "application/vnd.ms-outlook"));
        malwareScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanVerdict.Clean));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Document? saved = null;
        documents.Setup(d => d.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
        var result = await service.UploadAsync(
            new UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "mail.msg", "application/vnd.ms-outlook", content, content.Length),
            CancellationToken.None);

        result.State.Should().Be(DocumentState.Queued);
        saved.Should().NotBeNull();
        saved!.IsContainer.Should().BeTrue();
        saved.ContainerType.Should().Be(DocumentContainerType.Email);
        saved.ExtractionState.Should().Be(DocumentExtractionState.Pending);
    }

    [Fact]
    public async Task UploadAsync_WithoutRagProfile_QueuesDocumentForLexicalIndexing()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(true, "application/pdf"));
        malwareScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanVerdict.Clean));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream(new byte[] { 0x1, 0x2 });
        var result = await service.UploadAsync(new Harpyx.Application.DTOs.UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "file.pdf", "application/pdf", content, content.Length), CancellationToken.None);

        result.FileName.Should().Be("file.pdf");
        result.State.Should().Be(DocumentState.Queued);
        documents.Verify(d => d.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.UploadAsync("file.pdf", It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.EnqueueParseJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        usageLimits.Verify(p => p.EnsureRagIndexingAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_WithRagProfile_QueuesDocumentForIndexing()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                RagLlmOverride = LlmFeatureOverride.Enabled,
                RagEmbeddingModelId = Guid.NewGuid(),
                RagEmbeddingModel = new LlmModel
                {
                    Capability = LlmProviderType.RagEmbedding,
                    ModelId = "text-embedding-3-small",
                    IsEnabled = true,
                    Connection = new LlmConnection
                    {
                        Scope = LlmConnectionScope.Personal,
                        Provider = LlmProvider.OpenAI,
                        EncryptedApiKey = "encrypted-key",
                        IsEnabled = true
                    }
                },
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(true, "application/pdf"));
        malwareScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanVerdict.Clean));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        usageLimits.Setup(p => p.EnsureRagIndexingAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream(new byte[] { 0x1, 0x2 });
        var result = await service.UploadAsync(new Harpyx.Application.DTOs.UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "file.pdf", "application/pdf", content, content.Length), CancellationToken.None);

        result.State.Should().Be(DocumentState.Queued);
        queue.Verify(q => q.EnqueueParseJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WhenPolicyRejects_CreatesRejectedDocumentAndSkipsStorage()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(false, "application/octet-stream", "Extension not allowed."));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream(new byte[] { 0x1, 0x2 });
        var result = await service.UploadAsync(new UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "file.exe", "application/octet-stream", content, content.Length), CancellationToken.None);

        result.State.Should().Be(DocumentState.Rejected);
        storage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        queue.Verify(q => q.EnqueueParseJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        malwareScanner.Verify(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        documents.Verify(d => d.AddAsync(It.Is<Document>(x => x.State == DocumentState.Rejected), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.RecordAsync("upload_rejected_policy", null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WhenMalwareDetected_QuarantinesDocumentAndSkipsQueue()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                RagEmbeddingModelId = Guid.NewGuid(),
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(true, "application/pdf"));
        malwareScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanVerdict.Infected, "Eicar-Test-Signature", "FOUND"));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("quarantine-key");
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var result = await service.UploadAsync(new UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "file.pdf", "application/pdf", content, content.Length), CancellationToken.None);

        result.State.Should().Be(DocumentState.Quarantined);
        storage.Verify(s => s.UploadAsync("quarantine/file.pdf", It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.EnqueueParseJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        documents.Verify(d => d.AddAsync(It.Is<Document>(x => x.State == DocumentState.Quarantined), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.RecordAsync("upload_quarantined", null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WhenMalwareDetectedAndQuarantineDisabled_RejectsWithoutStorage()
    {
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var projects = new Mock<IProjectRepository>();
        var projectService = new Mock<IProjectService>();
        var storage = new Mock<IStorageService>();
        var queue = new Mock<IJobQueue>();
        var audit = new Mock<IAuditService>();
        var uploadPolicy = new Mock<IUploadSecurityPolicyService>();
        var malwareScanner = new Mock<IFileMalwareScanner>();
        var settings = new Mock<IPlatformSettingsService>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspaceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        projects.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Name = "Project",
                RagEmbeddingModelId = Guid.NewGuid(),
                Workspace = new Workspace { Id = workspaceId, TenantId = tenantId, Name = "Workspace" }
            });
        uploadPolicy.Setup(p => p.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadValidationResult(true, "application/pdf"));
        malwareScanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanVerdict.Infected, "Eicar-Test-Signature", "FOUND"));
        settings.Setup(s => s.IsQuarantineEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        usageLimits.Setup(p => p.EnsureDocumentUploadAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        projectService.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DocumentService(
            documents.Object,
            chunks.Object,
            projects.Object,
            projectService.Object,
            storage.Object,
            queue.Object,
            audit.Object,
            uploadPolicy.Object,
            malwareScanner.Object,
            settings.Object,
            usageLimits.Object,
            unitOfWork.Object);

        await using var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var result = await service.UploadAsync(new UploadDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "file.pdf", "application/pdf", content, content.Length), CancellationToken.None);

        result.State.Should().Be(DocumentState.Rejected);
        storage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        queue.Verify(q => q.EnqueueParseJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        documents.Verify(d => d.AddAsync(It.Is<Document>(x => x.State == DocumentState.Rejected), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.RecordAsync("upload_rejected_malware", null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
