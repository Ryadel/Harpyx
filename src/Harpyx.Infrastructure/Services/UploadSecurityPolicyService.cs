using Harpyx.Application.DTOs;
using Harpyx.Application.Defaults;
using Harpyx.Application.Interfaces;

namespace Harpyx.Infrastructure.Services;

public class UploadSecurityPolicyService : IUploadSecurityPolicyService
{
    private static readonly Dictionary<string, string[]> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".txt"] = ["text/plain"],
        [".md"] = ["text/markdown", "text/plain"],
        [".rtf"] = ["application/rtf", "text/rtf", "text/plain"],
        [".epub"] = ["application/epub+zip", "application/zip"],
        [".csv"] = ["text/csv", "text/plain"],
        [".json"] = ["application/json", "text/plain"],
        [".xml"] = ["application/xml", "text/xml", "text/plain"],
        [".yaml"] = ["application/yaml", "application/x-yaml", "text/yaml", "text/x-yaml", "text/plain"],
        [".yml"] = ["application/yaml", "application/x-yaml", "text/yaml", "text/x-yaml", "text/plain"],
        [".html"] = ["text/html", "text/plain"],
        [".htm"] = ["text/html", "text/plain"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip"],
        [".pptx"] = ["application/vnd.openxmlformats-officedocument.presentationml.presentation", "application/zip"],
        [".odt"] = ["application/vnd.oasis.opendocument.text", "application/zip"],
        [".ods"] = ["application/vnd.oasis.opendocument.spreadsheet", "application/zip"],
        [".odp"] = ["application/vnd.oasis.opendocument.presentation", "application/zip"],
        [".eml"] = ["message/rfc822", "text/plain"],
        [".msg"] = ["application/vnd.ms-outlook"],
        [".zip"] = ["application/zip", "application/x-zip-compressed"],
        [".rar"] = ["application/vnd.rar", "application/x-rar-compressed"],
        [".7z"] = ["application/x-7z-compressed"],
        [".tgz"] = ["application/gzip", "application/x-gzip"],
        [".tar.gz"] = ["application/gzip", "application/x-gzip"],
        [".png"] = ["image/png"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".tif"] = ["image/tiff"],
        [".tiff"] = ["image/tiff"],
        [".bmp"] = ["image/bmp"]
    };

    private static readonly HashSet<string> ZipBackedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx",
        ".xlsx",
        ".pptx",
        ".odt",
        ".ods",
        ".odp",
        ".epub"
    };

    private readonly UploadSecurityOptions _options;
    private readonly IPlatformSettingsRepository _platformSettings;
    private readonly HashSet<string> _allowedExtensions;
    private readonly HashSet<string> _allowedContentTypes;

    public UploadSecurityPolicyService(
        UploadSecurityOptions options,
        IPlatformSettingsRepository platformSettings)
    {
        _options = options;
        _platformSettings = platformSettings;
        _allowedExtensions = new HashSet<string>(
            (_options.AllowedExtensions ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);
        _allowedContentTypes = new HashSet<string>(
            (_options.AllowedContentTypes ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeContentType),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<UploadValidationResult> ValidateAsync(
        string fileName,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Reject("File name is required.");

        if (sizeBytes <= 0)
            return Reject("Empty files are not allowed.");

        var settings = await _platformSettings.GetAsync(cancellationToken);
        var maxFileSizeBytes = settings?.MaxFileSizeBytes > 0
            ? settings.MaxFileSizeBytes
            : UploadDefaults.MaxFileSizeBytes;
        if (maxFileSizeBytes > 0 && sizeBytes > maxFileSizeBytes)
            return Reject($"File exceeds the maximum allowed size ({maxFileSizeBytes} bytes).");

        var extension = ExtractAndNormalizeExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
            return Reject("File extension is missing.");

        if (_allowedExtensions.Count > 0 && !_allowedExtensions.Contains(extension))
            return Reject($"File extension '{extension}' is not allowed.");

        var declaredContentType = NormalizeContentType(contentType);
        if (_allowedContentTypes.Count > 0 && !string.IsNullOrWhiteSpace(declaredContentType) && !_allowedContentTypes.Contains(declaredContentType))
            return Reject($"Content type '{declaredContentType}' is not allowed.");

        var header = await ReadHeaderAsync(content, 512, cancellationToken);
        var detectedContentType = DetectContentType(header);

        var normalizedContentType = !string.IsNullOrWhiteSpace(detectedContentType)
            ? detectedContentType
            : !string.IsNullOrWhiteSpace(declaredContentType) &&
              !declaredContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                ? declaredContentType
                : ContentTypesByExtension.TryGetValue(extension, out var defaultTypes)
                    ? defaultTypes[0]
                    : "application/octet-stream";

        // OpenXML/OpenDocument/EPUB containers are ZIP-based; keep logical content-type by extension.
        if (normalizedContentType.Equals("application/zip", StringComparison.OrdinalIgnoreCase) &&
            ZipBackedExtensions.Contains(extension) &&
            ContentTypesByExtension.TryGetValue(extension, out var zipBasedTypes) &&
            zipBasedTypes.Length > 0)
        {
            normalizedContentType = zipBasedTypes[0];
        }

        if (_allowedContentTypes.Count > 0 && !_allowedContentTypes.Contains(normalizedContentType))
            return Reject($"Detected content type '{normalizedContentType}' is not allowed.");

        if (ContentTypesByExtension.TryGetValue(extension, out var extensionContentTypes) &&
            extensionContentTypes.All(x => !x.Equals(normalizedContentType, StringComparison.OrdinalIgnoreCase)))
        {
            return Reject($"File extension '{extension}' does not match detected content type '{normalizedContentType}'.");
        }

        if (!string.IsNullOrWhiteSpace(detectedContentType) &&
            !string.IsNullOrWhiteSpace(declaredContentType) &&
            !AreCompatibleContentTypes(declaredContentType, detectedContentType))
        {
            return Reject($"Declared content type '{declaredContentType}' does not match detected content type '{detectedContentType}'.");
        }

        return new UploadValidationResult(true, normalizedContentType);
    }

    private static UploadValidationResult Reject(string reason)
        => new(false, "application/octet-stream", reason);

    private static async Task<byte[]> ReadHeaderAsync(Stream content, int maxBytes, CancellationToken cancellationToken)
    {
        var header = new byte[maxBytes];
        var totalRead = 0;
        var originalPosition = content.CanSeek ? content.Position : 0;
        if (content.CanSeek)
            content.Position = 0;

        try
        {
            while (totalRead < maxBytes)
            {
                var read = await content.ReadAsync(header.AsMemory(totalRead, maxBytes - totalRead), cancellationToken);
                if (read == 0)
                    break;

                totalRead += read;
            }
        }
        finally
        {
            if (content.CanSeek)
                content.Position = originalPosition;
        }

        if (totalRead == header.Length)
            return header;

        return header[..totalRead];
    }

    private static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 6 &&
            header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC &&
            header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
            return "application/x-7z-compressed";

        if (header.Length >= 7 &&
            header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 &&
            header[3] == 0x21 && header[4] == 0x1A && header[5] == 0x07 &&
            (header[6] == 0x00 || header[6] == 0x01))
            return "application/vnd.rar";

        if (header.Length >= 4 &&
            header[0] == 0x50 && header[1] == 0x4B &&
            (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07) &&
            (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08))
            return "application/zip";

        if (header.Length >= 8 &&
            header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0 &&
            header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1)
            return "application/vnd.ms-outlook";

        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            return "application/gzip";

        if (header.Length >= 5 &&
            header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D)
            return "application/pdf";

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        if (header.Length >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (header.Length >= 4 &&
            ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
             (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)))
            return "image/tiff";

        if (header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D)
            return "image/bmp";

        if (LooksLikeText(header))
            return "text/plain";

        return null;
    }

    private static bool LooksLikeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return false;

        var printable = 0;
        foreach (var b in bytes)
        {
            if (b == 0x00)
                return false;

            if (b is 0x09 or 0x0A or 0x0D || (b >= 0x20 && b <= 0x7E))
            {
                printable++;
                continue;
            }

            if (b >= 0xC2)
            {
                printable++;
            }
        }

        return printable >= (int)(bytes.Length * 0.85);
    }

    private static string ExtractAndNormalizeExtension(string fileName)
    {
        var trimmed = fileName.Trim();
        if (trimmed.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return ".tar.gz";

        return NormalizeExtension(Path.GetExtension(trimmed));
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        var value = extension.Trim();
        return value.StartsWith('.') ? value.ToLowerInvariant() : "." + value.ToLowerInvariant();
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;

        var raw = contentType.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];
        return raw.ToLowerInvariant();
    }

    private static bool AreCompatibleContentTypes(string declared, string detected)
    {
        if (declared.Equals(detected, StringComparison.OrdinalIgnoreCase))
            return true;

        if (declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return true;

        if (declared.StartsWith("text/", StringComparison.OrdinalIgnoreCase) &&
            detected.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        if ((declared.Equals("application/xml", StringComparison.OrdinalIgnoreCase) &&
             detected.Equals("text/xml", StringComparison.OrdinalIgnoreCase)) ||
            (declared.Equals("text/xml", StringComparison.OrdinalIgnoreCase) &&
             detected.Equals("application/xml", StringComparison.OrdinalIgnoreCase)))
            return true;

        if ((declared.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) &&
             detected.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)) ||
            (declared.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) &&
             detected.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (detected.Equals("application/zip", StringComparison.OrdinalIgnoreCase) &&
            (declared.Contains("openxmlformats-officedocument", StringComparison.OrdinalIgnoreCase) ||
             declared.Contains("oasis.opendocument", StringComparison.OrdinalIgnoreCase) ||
             declared.Equals("application/epub+zip", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
