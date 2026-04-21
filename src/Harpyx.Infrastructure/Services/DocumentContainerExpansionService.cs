using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using SharpCompress.Archives;

namespace Harpyx.Infrastructure.Services;

public interface IDocumentContainerExpansionService
{
    Task<ContainerExpansionResult> ExpandAsync(Document containerDocument, PlatformSettingsDto runtime, CancellationToken cancellationToken);
}

public sealed record ContainerExpansionResult(bool HasWarnings);

public class DocumentContainerExpansionService : IDocumentContainerExpansionService
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".rar",
        ".7z",
        ".tgz",
        ".tar.gz",
        ".gz"
    };

    private static readonly HashSet<string> ArchiveContentTypes = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".txt", ".md", ".rtf", ".epub", ".csv", ".json", ".xml", ".yaml", ".yml", ".html", ".htm",
        ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp", ".eml", ".msg", ".zip", ".rar", ".7z", ".tar.gz", ".tgz",
        ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"
    };

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".rtf"] = "application/rtf",
        [".epub"] = "application/epub+zip",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".eml"] = "message/rfc822",
        [".msg"] = "application/vnd.ms-outlook",
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
        [".7z"] = "application/x-7z-compressed",
        [".tgz"] = "application/gzip",
        [".tar.gz"] = "application/gzip",
        [".gz"] = "application/gzip",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".bmp"] = "image/bmp"
    };

    private readonly IStorageService _storage;
    private readonly IDocumentRepository _documents;
    private readonly IJobQueue _jobQueue;
    private readonly IUnitOfWork _unitOfWork;

    public DocumentContainerExpansionService(
        IStorageService storage,
        IDocumentRepository documents,
        IJobQueue jobQueue,
        IUnitOfWork unitOfWork)
    {
        _storage = storage;
        _documents = documents;
        _jobQueue = jobQueue;
        _unitOfWork = unitOfWork;
    }

    public async Task<ContainerExpansionResult> ExpandAsync(Document containerDocument, PlatformSettingsDto runtime, CancellationToken cancellationToken)
    {
        if (!containerDocument.IsContainer)
            return new ContainerExpansionResult(false);

        return containerDocument.ContainerType switch
        {
            DocumentContainerType.Archive => await ExpandArchiveAsync(containerDocument, runtime, cancellationToken),
            DocumentContainerType.Email => await ExpandEmailAsync(containerDocument, runtime, cancellationToken),
            _ => new ContainerExpansionResult(false)
        };
    }

    private async Task<ContainerExpansionResult> ExpandArchiveAsync(Document container, PlatformSettingsDto runtime, CancellationToken cancellationToken)
    {
        var limits = ContainerExtractionLimits.From(runtime);
        var rootContainerId = ResolveRootContainerId(container);
        var originatingUploadId = ResolveOriginatingUploadId(container);

        var (existingPaths, persistedPaths) = await LoadContainerPathsAsync(container.Id, cancellationToken);
        var stats = await _documents.GetExtractionStatsByRootAsync(rootContainerId, cancellationToken);
        var counters = new RootExtractionCounters(stats.FileCount, stats.TotalSizeBytes);

        var hasWarnings = false;

        await using var content = await _storage.OpenReadAsync(container.StorageKey, cancellationToken);
        using var archive = ArchiveFactory.OpenArchive(content);

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (counters.FileCount >= limits.MaxFilesPerRoot)
            {
                hasWarnings = true;
                break;
            }

            var remainingBytes = limits.MaxTotalExtractedBytesPerRoot - counters.TotalSizeBytes;
            if (remainingBytes <= 0)
            {
                hasWarnings = true;
                break;
            }

            var rawPath = NormalizeContainerPathValue(entry.Key ?? string.Empty);
            if (string.IsNullOrWhiteSpace(rawPath))
                continue;

            var proposedContainerPath = ComposeContainerPath(container.ContainerPath, rawPath);
            if (persistedPaths.Contains(proposedContainerPath))
                continue;

            var containerPath = EnsureUniqueContainerPath(proposedContainerPath, existingPaths);
            var fileName = ResolveDisplayFileName(containerPath);
            var contentType = GuessContentType(fileName, null);
            var childNestingLevel = container.NestingLevel + 1;

            if (childNestingLevel > limits.MaxNestingDepth)
            {
                hasWarnings = true;
                await PersistBlockedChildAsync(
                    container,
                    fileName,
                    contentType,
                    rootContainerId,
                    originatingUploadId,
                    childNestingLevel,
                    containerPath,
                    cancellationToken);
                counters.FileCount++;
                continue;
            }

            if (entry.IsEncrypted || entry.Size > limits.MaxSingleEntrySizeBytes)
            {
                hasWarnings = true;
                await PersistBlockedChildAsync(
                    container,
                    fileName,
                    contentType,
                    rootContainerId,
                    originatingUploadId,
                    childNestingLevel,
                    containerPath,
                    cancellationToken);
                counters.FileCount++;
                continue;
            }

            if (entry.Size > 0 && entry.Size > remainingBytes)
            {
                hasWarnings = true;
                break;
            }

            await using var entryStream = entry.OpenEntryStream();
            var maxReadableBytes = Math.Min(limits.MaxSingleEntrySizeBytes, remainingBytes);
            var readResult = await ReadToMemoryWithLimitAsync(entryStream, maxReadableBytes, cancellationToken);
            if (readResult.LimitExceeded)
            {
                hasWarnings = true;
                if (remainingBytes <= limits.MaxSingleEntrySizeBytes)
                    break;

                await PersistBlockedChildAsync(
                    container,
                    fileName,
                    contentType,
                    rootContainerId,
                    originatingUploadId,
                    childNestingLevel,
                    containerPath,
                    cancellationToken);
                counters.FileCount++;
                continue;
            }

            await using var payload = readResult.Content!;
            var payloadSize = payload.Length;
            if (counters.TotalSizeBytes + payloadSize > limits.MaxTotalExtractedBytesPerRoot)
            {
                hasWarnings = true;
                break;
            }

            contentType = GuessContentType(fileName, contentType);
            var metadata = ResolveContainerMetadata(fileName, contentType);
            var supported = IsSupportedForPipeline(fileName, contentType);
            if (!supported)
            {
                var unsupported = CreateChildDocument(
                    container,
                    fileName,
                    contentType,
                    payloadSize,
                    string.Empty,
                    rootContainerId,
                    originatingUploadId,
                    childNestingLevel,
                    containerPath,
                    DocumentState.Completed,
                    DocumentExtractionState.Unsupported,
                    metadata.IsContainer,
                    metadata.ContainerType);
                await PersistChildAsync(unsupported, enqueueForProcessing: false, cancellationToken);
            }
            else
            {
                payload.Position = 0;
                var storageKey = await _storage.UploadAsync(fileName, payload, contentType, cancellationToken);
                var queued = CreateChildDocument(
                    container,
                    fileName,
                    contentType,
                    payloadSize,
                    storageKey,
                    rootContainerId,
                    originatingUploadId,
                    childNestingLevel,
                    containerPath,
                    DocumentState.Queued,
                    DocumentExtractionState.Pending,
                    metadata.IsContainer,
                    metadata.ContainerType);
                await PersistChildAsync(queued, enqueueForProcessing: true, cancellationToken);
            }

            counters.FileCount++;
            counters.TotalSizeBytes += payloadSize;
        }

        return new ContainerExpansionResult(hasWarnings);
    }

    private async Task<ContainerExpansionResult> ExpandEmailAsync(Document container, PlatformSettingsDto runtime, CancellationToken cancellationToken)
    {
        var limits = ContainerExtractionLimits.From(runtime);
        var rootContainerId = ResolveRootContainerId(container);
        var originatingUploadId = ResolveOriginatingUploadId(container);

        var (existingPaths, persistedPaths) = await LoadContainerPathsAsync(container.Id, cancellationToken);
        var stats = await _documents.GetExtractionStatsByRootAsync(rootContainerId, cancellationToken);
        var counters = new RootExtractionCounters(stats.FileCount, stats.TotalSizeBytes);

        var hasWarnings = false;
        try
        {
            await using var content = await _storage.OpenReadAsync(container.StorageKey, cancellationToken);
            var message = await NormalizedEmailMessageParser.ParseAsync(
                container.FileName,
                container.ContentType,
                content,
                cancellationToken);
            var attachmentNumber = 0;

            foreach (var attachment in message.Attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attachmentNumber++;

                if (counters.FileCount >= limits.MaxFilesPerRoot)
                {
                    hasWarnings = true;
                    break;
                }

                var remainingBytes = limits.MaxTotalExtractedBytesPerRoot - counters.TotalSizeBytes;
                if (remainingBytes <= 0)
                {
                    hasWarnings = true;
                    break;
                }

                var attachmentFileName = ResolveAttachmentFileName(attachment.FileName, attachmentNumber);
                var proposedContainerPath = ComposeContainerPath(container.ContainerPath, attachmentFileName);
                if (persistedPaths.Contains(proposedContainerPath))
                    continue;

                var containerPath = EnsureUniqueContainerPath(proposedContainerPath, existingPaths);
                var fileName = ResolveDisplayFileName(containerPath);
                var childNestingLevel = container.NestingLevel + 1;
                var declaredContentType = NormalizeContentType(attachment.ContentType);
                var contentType = GuessContentType(fileName, declaredContentType);

                if (childNestingLevel > limits.MaxNestingDepth)
                {
                    hasWarnings = true;
                    await PersistBlockedChildAsync(
                        container,
                        fileName,
                        contentType,
                        rootContainerId,
                        originatingUploadId,
                        childNestingLevel,
                        containerPath,
                        cancellationToken);
                    counters.FileCount++;
                    continue;
                }

                await using var payload = new MemoryStream(attachment.Data, writable: false);
                if (payload.Length > limits.MaxSingleEntrySizeBytes)
                {
                    hasWarnings = true;
                    await PersistBlockedChildAsync(
                        container,
                        fileName,
                        contentType,
                        rootContainerId,
                        originatingUploadId,
                        childNestingLevel,
                        containerPath,
                        cancellationToken);
                    counters.FileCount++;
                    continue;
                }

                if (payload.Length > remainingBytes)
                {
                    hasWarnings = true;
                    break;
                }

                var metadata = ResolveContainerMetadata(fileName, contentType);
                var supported = IsSupportedForPipeline(fileName, contentType);
                if (!supported)
                {
                    var unsupported = CreateChildDocument(
                        container,
                        fileName,
                        contentType,
                        payload.Length,
                        string.Empty,
                        rootContainerId,
                        originatingUploadId,
                        childNestingLevel,
                        containerPath,
                        DocumentState.Completed,
                        DocumentExtractionState.Unsupported,
                        metadata.IsContainer,
                        metadata.ContainerType);
                    await PersistChildAsync(unsupported, enqueueForProcessing: false, cancellationToken);
                }
                else
                {
                    payload.Position = 0;
                    var storageKey = await _storage.UploadAsync(fileName, payload, contentType, cancellationToken);
                    var queued = CreateChildDocument(
                        container,
                        fileName,
                        contentType,
                        payload.Length,
                        storageKey,
                        rootContainerId,
                        originatingUploadId,
                        childNestingLevel,
                        containerPath,
                        DocumentState.Queued,
                        DocumentExtractionState.Pending,
                        metadata.IsContainer,
                        metadata.ContainerType);
                    await PersistChildAsync(queued, enqueueForProcessing: true, cancellationToken);
                }

                counters.FileCount++;
                counters.TotalSizeBytes += payload.Length;
            }
        }
        catch
        {
            hasWarnings = true;
        }

        return new ContainerExpansionResult(hasWarnings);
    }

    private async Task<(HashSet<string> KnownPaths, HashSet<string> PersistedPaths)> LoadContainerPathsAsync(
        Guid parentDocumentId,
        CancellationToken cancellationToken)
    {
        var existingChildren = await _documents.GetByParentDocumentIdAsync(parentDocumentId, cancellationToken);
        var persistedPaths = new HashSet<string>(
            existingChildren
                .Select(x => x.ContainerPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))!
                .Select(x => NormalizeContainerPathValue(x!)),
            StringComparer.OrdinalIgnoreCase);

        return (new HashSet<string>(persistedPaths, StringComparer.OrdinalIgnoreCase), persistedPaths);
    }

    private async Task PersistBlockedChildAsync(
        Document parent,
        string fileName,
        string contentType,
        Guid rootContainerId,
        Guid originatingUploadId,
        int nestingLevel,
        string containerPath,
        CancellationToken cancellationToken)
    {
        var blocked = CreateChildDocument(
            parent,
            fileName,
            contentType,
            0,
            string.Empty,
            rootContainerId,
            originatingUploadId,
            nestingLevel,
            containerPath,
            DocumentState.Rejected,
            DocumentExtractionState.BlockedByPolicy,
            isContainer: false,
            containerType: DocumentContainerType.None);

        await PersistChildAsync(blocked, enqueueForProcessing: false, cancellationToken);
    }

    private async Task PersistChildAsync(Document child, bool enqueueForProcessing, CancellationToken cancellationToken)
    {
        await _documents.AddAsync(child, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (enqueueForProcessing)
            await _jobQueue.EnqueueParseJobAsync(child.Id, cancellationToken);
    }

    private static Document CreateChildDocument(
        Document parent,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        Guid rootContainerId,
        Guid originatingUploadId,
        int nestingLevel,
        string containerPath,
        DocumentState state,
        DocumentExtractionState extractionState,
        bool isContainer,
        DocumentContainerType containerType)
    {
        return new Document
        {
            UploadedByUserId = parent.UploadedByUserId,
            ProjectId = parent.ProjectId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            State = state,
            ParentDocumentId = parent.Id,
            RootContainerDocumentId = rootContainerId,
            NestingLevel = nestingLevel,
            ContainerPath = containerPath,
            IsContainer = isContainer,
            ContainerType = containerType,
            ExtractionState = extractionState,
            OriginatingUploadId = originatingUploadId
        };
    }

    private static async Task<ReadToMemoryResult> ReadToMemoryWithLimitAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        if (maxBytes <= 0)
            return new ReadToMemoryResult(null, true);

        var memory = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;

            if (memory.Length + read > maxBytes)
            {
                await memory.DisposeAsync();
                return new ReadToMemoryResult(null, true);
            }

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        memory.Position = 0;
        return new ReadToMemoryResult(memory, false);
    }

    private static string ResolveAttachmentFileName(string? fileName, int index)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return $"attachment-{index}";

        return NormalizeContainerPathValue(fileName);
    }

    private static string EnsureUniqueContainerPath(string candidatePath, HashSet<string> knownPaths)
    {
        if (knownPaths.Add(candidatePath))
            return candidatePath;

        var normalized = NormalizeContainerPathValue(candidatePath);
        var directory = GetDirectory(normalized);
        var fileName = Path.GetFileName(normalized);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var i = 2; ; i++)
        {
            var candidateFile = string.IsNullOrWhiteSpace(extension)
                ? $"{baseName} ({i})"
                : $"{baseName} ({i}){extension}";
            var candidate = string.IsNullOrWhiteSpace(directory)
                ? candidateFile
                : $"{directory}/{candidateFile}";
            if (knownPaths.Add(candidate))
                return candidate;
        }
    }

    private static string ComposeContainerPath(string? parentContainerPath, string childPath)
    {
        var normalizedChild = NormalizeContainerPathValue(childPath);
        if (string.IsNullOrWhiteSpace(parentContainerPath))
            return normalizedChild;

        var normalizedParent = NormalizeContainerPathValue(parentContainerPath);
        return string.IsNullOrWhiteSpace(normalizedParent)
            ? normalizedChild
            : $"{normalizedParent}/{normalizedChild}";
    }

    private static string ResolveDisplayFileName(string containerPath)
    {
        var normalized = NormalizeContainerPathValue(containerPath);
        var fileName = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(fileName)
            ? $"extracted-{Guid.NewGuid():N}"
            : fileName;
    }

    private static string GetDirectory(string normalizedPath)
    {
        var idx = normalizedPath.LastIndexOf('/');
        return idx <= 0 ? string.Empty : normalizedPath[..idx];
    }

    private static Guid ResolveRootContainerId(Document document)
        => document.RootContainerDocumentId ?? document.Id;

    private static Guid ResolveOriginatingUploadId(Document document)
        => document.OriginatingUploadId ?? ResolveRootContainerId(document);

    private static (bool IsContainer, DocumentContainerType ContainerType) ResolveContainerMetadata(string fileName, string contentType)
    {
        var extension = GetEffectiveExtension(fileName);
        if (ArchiveExtensions.Contains(extension) || ArchiveContentTypes.Contains(contentType))
            return (true, DocumentContainerType.Archive);

        if (EmailExtensions.Contains(extension) || EmailContentTypes.Contains(contentType))
            return (true, DocumentContainerType.Email);

        return (false, DocumentContainerType.None);
    }

    private static bool IsSupportedForPipeline(string fileName, string contentType)
    {
        var extension = GetEffectiveExtension(fileName);
        if (SupportedExtensions.Contains(extension))
            return true;

        if (!string.IsNullOrWhiteSpace(contentType) &&
            (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
             ContentTypeByExtension.Values.Contains(contentType, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string GuessContentType(string fileName, string? contentType)
    {
        var normalizedDeclared = NormalizeContentType(contentType);
        if (!string.IsNullOrWhiteSpace(normalizedDeclared) &&
            !normalizedDeclared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedDeclared;
        }

        var extension = GetEffectiveExtension(fileName);
        if (ContentTypeByExtension.TryGetValue(extension, out var resolved))
            return resolved;

        return "application/octet-stream";
    }

    private static string GetEffectiveExtension(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return ".tar.gz";

        return Path.GetExtension(fileName);
    }

    private static string NormalizeContainerPathValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Replace('\\', '/').Trim();
        while (normalized.StartsWith('/'))
            normalized = normalized[1..];

        return normalized;
    }

    private static string NormalizeContentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0]
            .ToLowerInvariant();
    }

    private sealed record ReadToMemoryResult(MemoryStream? Content, bool LimitExceeded);

    private sealed class RootExtractionCounters
    {
        public RootExtractionCounters(int fileCount, long totalSizeBytes)
        {
            FileCount = fileCount;
            TotalSizeBytes = totalSizeBytes;
        }

        public int FileCount { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    private sealed record ContainerExtractionLimits(
        int MaxNestingDepth,
        long MaxTotalExtractedBytesPerRoot,
        int MaxFilesPerRoot,
        long MaxSingleEntrySizeBytes)
    {
        public static ContainerExtractionLimits From(PlatformSettingsDto runtime)
            => new(
                Math.Max(1, runtime.ContainerMaxNestingDepth),
                Math.Max(25L * 1024L * 1024L, runtime.ContainerMaxTotalExtractedBytesPerRoot),
                Math.Max(1, runtime.ContainerMaxFilesPerRoot),
                Math.Max(1L * 1024L * 1024L, runtime.ContainerMaxSingleEntrySizeBytes));
    }
}
