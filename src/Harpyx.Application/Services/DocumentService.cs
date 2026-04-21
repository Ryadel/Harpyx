using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class DocumentService : IDocumentService
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".rar",
        ".7z",
        ".tgz",
        ".gz"
    };

    private static readonly HashSet<string> ArchiveLikeContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/zip",
        "application/x-zip-compressed",
        "application/vnd.rar",
        "application/x-rar-compressed",
        "application/x-7z-compressed",
        "application/gzip",
        "application/x-gzip",
        "application/x-tar"
    };

    private static readonly HashSet<string> EmailExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".eml",
        ".msg"
    };

    private static readonly HashSet<string> EmailContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "message/rfc822",
        "application/vnd.ms-outlook"
    };

    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectService _projectService;
    private readonly IStorageService _storage;
    private readonly IJobQueue _jobQueue;
    private readonly IAuditService _auditService;
    private readonly IUploadSecurityPolicyService _uploadPolicy;
    private readonly IFileMalwareScanner _malwareScanner;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IUsageLimitService _usageLimits;
    private readonly IUnitOfWork _unitOfWork;

    public DocumentService(
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IProjectRepository projectRepository,
        IProjectService projectService,
        IStorageService storage,
        IJobQueue jobQueue,
        IAuditService auditService,
        IUploadSecurityPolicyService uploadPolicy,
        IFileMalwareScanner malwareScanner,
        IPlatformSettingsService platformSettings,
        IUsageLimitService usageLimits,
        IUnitOfWork unitOfWork)
    {
        _documents = documents;
        _chunks = chunks;
        _projectRepository = projectRepository;
        _projectService = projectService;
        _storage = storage;
        _jobQueue = jobQueue;
        _auditService = auditService;
        _uploadPolicy = uploadPolicy;
        _malwareScanner = malwareScanner;
        _platformSettings = platformSettings;
        _usageLimits = usageLimits;
        _unitOfWork = unitOfWork;
    }

    public async Task<DocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project not found.");
        await _usageLimits.EnsureDocumentUploadAllowedAsync(
            request.UploadedByUserId,
            project.Workspace.TenantId,
            project.WorkspaceId,
            request.SizeBytes,
            cancellationToken);
        var effectiveRagModelId = ResolveEffectiveRagModelId(project);
        if (effectiveRagModelId is not null)
        {
            await _usageLimits.EnsureRagIndexingAllowedAsync(project.Workspace.TenantId, cancellationToken);
        }

        var bufferedStream = request.Content.CanSeek ? null : new MemoryStream();
        var content = request.Content;
        if (bufferedStream is not null)
        {
            await request.Content.CopyToAsync(bufferedStream, cancellationToken);
            bufferedStream.Position = 0;
            content = bufferedStream;
        }

        try
        {
            var validation = await _uploadPolicy.ValidateAsync(
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                content,
                cancellationToken);
            var (isContainer, containerType) = ResolveContainerMetadata(request.FileName, validation.NormalizedContentType);

            if (!validation.IsAccepted)
            {
                var rejectedDocument = new Document
                {
                    UploadedByUserId = request.UploadedByUserId,
                    ProjectId = project.Id,
                    FileName = request.FileName,
                    ContentType = validation.NormalizedContentType,
                    SizeBytes = request.SizeBytes,
                    StorageKey = string.Empty,
                    State = DocumentState.Rejected,
                    IsContainer = isContainer,
                    ContainerType = containerType,
                    NestingLevel = 0,
                    ExtractionState = DocumentExtractionState.BlockedByPolicy
                };
                rejectedDocument.OriginatingUploadId = rejectedDocument.Id;

                await _documents.AddAsync(rejectedDocument, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _auditService.RecordAsync(
                    "upload_rejected_policy",
                    null,
                    null,
                    $"Document {rejectedDocument.Id} rejected by upload policy. Reason: {validation.RejectionReason}",
                    cancellationToken);
                await _projectService.TouchLifetimeAsync(project.Id, cancellationToken);

                return ToDto(rejectedDocument);
            }

            var scan = await _malwareScanner.ScanAsync(
                request.FileName,
                validation.NormalizedContentType,
                content,
                cancellationToken);

            if (scan.Verdict is MalwareScanVerdict.Infected or MalwareScanVerdict.Error)
            {
                var quarantineEnabled = await _platformSettings.IsQuarantineEnabledAsync(cancellationToken);

                if (!quarantineEnabled)
                {
                    var rejectedByMalware = new Document
                    {
                        UploadedByUserId = request.UploadedByUserId,
                        ProjectId = project.Id,
                        FileName = request.FileName,
                        ContentType = validation.NormalizedContentType,
                        SizeBytes = request.SizeBytes,
                        StorageKey = string.Empty,
                        State = DocumentState.Rejected,
                        IsContainer = isContainer,
                        ContainerType = containerType,
                        NestingLevel = 0,
                        ExtractionState = DocumentExtractionState.BlockedByPolicy
                    };
                    rejectedByMalware.OriginatingUploadId = rejectedByMalware.Id;

                    await _documents.AddAsync(rejectedByMalware, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _auditService.RecordAsync(
                        "upload_rejected_malware",
                        null,
                        null,
                        $"Document {rejectedByMalware.Id} rejected by malware scan. Verdict: {scan.Verdict}. Signature: {scan.Signature ?? "n/a"}. Details: {scan.Details ?? "n/a"}",
                        cancellationToken);
                    await _projectService.TouchLifetimeAsync(project.Id, cancellationToken);

                    return ToDto(rejectedByMalware);
                }

                if (content.CanSeek)
                    content.Position = 0;

                var quarantineStorageKey = await _storage.UploadAsync(
                    $"quarantine/{request.FileName}",
                    content,
                    validation.NormalizedContentType,
                    cancellationToken);

                var quarantinedDocument = new Document
                {
                    UploadedByUserId = request.UploadedByUserId,
                    ProjectId = project.Id,
                    FileName = request.FileName,
                    ContentType = validation.NormalizedContentType,
                    SizeBytes = request.SizeBytes,
                    StorageKey = quarantineStorageKey,
                    State = DocumentState.Quarantined,
                    IsContainer = isContainer,
                    ContainerType = containerType,
                    NestingLevel = 0,
                    ExtractionState = DocumentExtractionState.BlockedByPolicy
                };
                quarantinedDocument.OriginatingUploadId = quarantinedDocument.Id;

                await _documents.AddAsync(quarantinedDocument, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _auditService.RecordAsync(
                    "upload_quarantined",
                    null,
                    null,
                    $"Document {quarantinedDocument.Id} quarantined. Verdict: {scan.Verdict}. Signature: {scan.Signature ?? "n/a"}. Details: {scan.Details ?? "n/a"}",
                    cancellationToken);
                await _projectService.TouchLifetimeAsync(project.Id, cancellationToken);

                return ToDto(quarantinedDocument);
            }

            if (content.CanSeek)
                content.Position = 0;

            var storageKey = await _storage.UploadAsync(request.FileName, content, validation.NormalizedContentType, cancellationToken);
            var document = new Document
            {
                UploadedByUserId = request.UploadedByUserId,
                ProjectId = project.Id,
                FileName = request.FileName,
                ContentType = validation.NormalizedContentType,
                SizeBytes = request.SizeBytes,
                StorageKey = storageKey,
                State = DocumentState.Queued,
                IsContainer = isContainer,
                ContainerType = containerType,
                NestingLevel = 0,
                ExtractionState = DocumentExtractionState.Pending
            };
            document.OriginatingUploadId = document.Id;

            await _documents.AddAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _auditService.RecordAsync("upload", null, null, $"Document {document.Id} uploaded", cancellationToken);
            await _jobQueue.EnqueueParseJobAsync(document.Id, cancellationToken);
            await _projectService.TouchLifetimeAsync(project.Id, cancellationToken);

            return ToDto(document);
        }
        finally
        {
            if (bufferedStream is not null)
                await bufferedStream.DisposeAsync();
        }
    }

    public async Task<DocumentDto> AddUrlAsync(AddUrlDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("Invalid URL. Only http and https URLs are supported.");

        var urlEnabled = await _platformSettings.IsUrlDocumentsEnabledAsync(cancellationToken);
        if (!urlEnabled)
            throw new InvalidOperationException("URL documents are disabled by the administrator.");

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project not found.");

        var document = new Document
        {
            UploadedByUserId = request.UploadedByUserId,
            ProjectId = project.Id,
            FileName = uri.Host + uri.AbsolutePath,
            ContentType = "text/html",
            SizeBytes = 0,
            StorageKey = string.Empty,
            SourceUrl = request.Url,
            State = DocumentState.Queued,
            IsContainer = false,
            ContainerType = DocumentContainerType.None,
            NestingLevel = 0,
            ExtractionState = DocumentExtractionState.Pending
        };
        document.OriginatingUploadId = document.Id;

        await _documents.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.RecordAsync("url_added", null, null,
            $"Document {document.Id} added from URL: {request.Url}", cancellationToken);
        await _jobQueue.EnqueueParseJobAsync(document.Id, cancellationToken);
        await _projectService.TouchLifetimeAsync(project.Id, cancellationToken);

        return ToDto(document);
    }

    public async Task<IReadOnlyList<DocumentDto>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var documents = await _documents.GetByProjectAsync(projectId, cancellationToken);
        return documents.Select(ToDto).ToList();
    }

    public async Task<DocumentDto?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documents.GetByIdAsync(documentId, cancellationToken);
        return document is null ? null : ToDto(document);
    }

    public async Task RenameAsync(Guid documentId, string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        var document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null) return;

        document.FileName = fileName.Trim();
        document.UpdatedAt = DateTimeOffset.UtcNow;
        _documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.RecordAsync("document_rename", null, null, $"Document {document.Id} renamed", cancellationToken);
        await _projectService.TouchLifetimeAsync(document.ProjectId, cancellationToken);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null) return;

        // Recursively delete all children first
        var children = await _documents.GetByParentDocumentIdAsync(documentId, cancellationToken);
        foreach (var child in children)
            await DeleteAsync(child.Id, cancellationToken);

        // Delete chunks
        await _chunks.RemoveByDocumentIdAsync(documentId, cancellationToken);

        // Delete storage
        if (!string.IsNullOrWhiteSpace(document.StorageKey))
            await _storage.DeleteAsync(document.StorageKey, cancellationToken);

        // Delete document record
        _documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.RecordAsync("document_delete", null, null, $"Document {document.Id} deleted", cancellationToken);
    }

    public async Task<bool> RetryAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
            return false;

        if (document.State != DocumentState.Failed)
            throw new InvalidOperationException("Only failed documents can be retried.");

        document.State = DocumentState.Queued;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        _documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.RecordAsync("document_retry", null, null, $"Document {document.Id} retried", cancellationToken);
        await _jobQueue.EnqueueParseJobAsync(document.Id, cancellationToken);
        await _projectService.TouchLifetimeAsync(document.ProjectId, cancellationToken);

        return true;
    }

    private static Guid? ResolveEffectiveRagModelId(Project project)
    {
        return project.RagLlmOverride switch
        {
            LlmFeatureOverride.Enabled => IsRagModelConfigured(project.RagEmbeddingModelId, project.RagEmbeddingModel)
                ? project.RagEmbeddingModelId
                : null,
            LlmFeatureOverride.Disabled => null,
            _ => IsRagModelConfigured(project.Workspace.RagEmbeddingModelId, project.Workspace.RagEmbeddingModel) &&
                 project.Workspace.IsRagLlmEnabled
                ? project.Workspace.RagEmbeddingModelId
                : null
        };
    }

    private static bool IsRagModelConfigured(Guid? modelId, LlmModel? model)
        => modelId is not null &&
           model is not null &&
           model.Capability == LlmProviderType.RagEmbedding &&
           model.IsEnabled &&
           model.Connection.IsEnabled &&
           model.Connection.Provider != LlmProvider.None &&
           (model.Connection.Scope == LlmConnectionScope.Hosted
                ? model.IsPublished
                : !string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey));

    private static (bool IsContainer, DocumentContainerType ContainerType) ResolveContainerMetadata(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (ArchiveExtensions.Contains(extension) || ArchiveLikeContentTypes.Contains(contentType))
            return (true, DocumentContainerType.Archive);

        if (EmailExtensions.Contains(extension) || EmailContentTypes.Contains(contentType))
            return (true, DocumentContainerType.Email);

        return (false, DocumentContainerType.None);
    }

    private static DocumentDto ToDto(Document d) => new(
        d.Id, d.ProjectId, d.FileName, d.ContentType,
        d.SizeBytes, d.Version, d.State,
        d.ParentDocumentId, d.RootContainerDocumentId,
        d.NestingLevel, d.ContainerPath,
        d.IsContainer, d.ContainerType,
        d.ExtractionState, d.OriginatingUploadId,
        d.SourceUrl);
}
