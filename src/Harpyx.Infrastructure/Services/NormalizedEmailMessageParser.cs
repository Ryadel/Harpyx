using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MsgReader.Outlook;

namespace Harpyx.Infrastructure.Services;

internal sealed record NormalizedEmailAttachment(
    string FileName,
    string ContentType,
    byte[] Data);

internal sealed record NormalizedEmailMessage(
    string Subject,
    string? From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    DateTimeOffset? SentOn,
    string Body,
    IReadOnlyList<NormalizedEmailAttachment> Attachments);

internal static class NormalizedEmailMessageParser
{
    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".eml"] = "message/rfc822",
        [".msg"] = "application/vnd.ms-outlook",
        [".txt"] = "text/plain",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml"
    };

    public static async Task<NormalizedEmailMessage> ParseAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var extension = GetEffectiveExtension(fileName);
        var normalizedContentType = NormalizeContentType(contentType);
        if (content.CanSeek)
            content.Position = 0;

        if (IsMsg(normalizedContentType, extension))
            return await ParseMsgAsync(content, cancellationToken);

        return await ParseEmlAsync(content, cancellationToken);
    }

    private static async Task<NormalizedEmailMessage> ParseEmlAsync(Stream content, CancellationToken cancellationToken)
    {
        var message = await MimeMessage.LoadAsync(content, cancellationToken);
        var subject = NormalizeInlineText(message.Subject ?? string.Empty);
        var from = message.From.Mailboxes.Select(FormatMailbox).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var to = message.To.Mailboxes
            .Select(FormatMailbox)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
        var cc = message.Cc.Mailboxes
            .Select(FormatMailbox)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
        DateTimeOffset? sentOn = message.Date != DateTimeOffset.MinValue ? message.Date.ToUniversalTime() : null;
        var body = !string.IsNullOrWhiteSpace(message.TextBody)
            ? message.TextBody
            : StripHtmlToText(message.HtmlBody);

        var attachments = new List<NormalizedEmailAttachment>();
        var index = 0;
        foreach (var attachment in message.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            var (attachmentFileName, attachmentContentType, attachmentData) = await ReadMimeAttachmentAsync(attachment, index, cancellationToken);
            attachments.Add(new NormalizedEmailAttachment(attachmentFileName, attachmentContentType, attachmentData));
        }

        return new NormalizedEmailMessage(
            string.IsNullOrWhiteSpace(subject) ? "(no subject)" : subject,
            from,
            to,
            cc,
            sentOn,
            NormalizeStructuredText(body),
            attachments);
    }

    private static async Task<NormalizedEmailMessage> ParseMsgAsync(Stream source, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msg");
        try
        {
            if (source.CanSeek)
                source.Position = 0;

            await using (var file = File.Create(tempPath))
            {
                await source.CopyToAsync(file, cancellationToken);
            }

            using var message = new Storage.Message(tempPath);
            var subject = NormalizeInlineText(message.Subject ?? string.Empty);
            var from = FormatMailbox(message.Sender?.DisplayName, message.Sender?.Email);
            var to = SplitRecipients(message.GetEmailRecipients(RecipientType.To, false, false));
            var cc = SplitRecipients(message.GetEmailRecipients(RecipientType.Cc, false, false));
            var body = !string.IsNullOrWhiteSpace(message.BodyText)
                ? message.BodyText
                : StripHtmlToText(message.BodyHtml);

            var attachments = new List<NormalizedEmailAttachment>();
            var index = 0;
            foreach (var attachmentObject in message.Attachments ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
                switch (attachmentObject)
                {
                    case Storage.Attachment attachment:
                    {
                        var fileName = ResolveMsgAttachmentFileName(attachment.FileName, index, ".bin");
                        var resolvedContentType = GuessContentType(fileName, attachment.MimeType);
                        var data = attachment.Data ?? Array.Empty<byte>();
                        attachments.Add(new NormalizedEmailAttachment(fileName, resolvedContentType, data));
                        break;
                    }
                    case Storage.Message attachedMessage:
                    {
                        await using var memory = new MemoryStream();
                        attachedMessage.Save(memory);
                        var nestedData = memory.ToArray();
                        var fileName = ResolveMsgAttachmentFileName(
                            attachedMessage.FileName,
                            index,
                            ".msg",
                            attachedMessage.Subject);
                        attachments.Add(new NormalizedEmailAttachment(fileName, "application/vnd.ms-outlook", nestedData));
                        break;
                    }
                }
            }

            return new NormalizedEmailMessage(
                string.IsNullOrWhiteSpace(subject) ? "(no subject)" : subject,
                from,
                to,
                cc,
                message.SentOn?.ToUniversalTime(),
                NormalizeStructuredText(body),
                attachments);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup for temp msg files.
            }
        }
    }

    private static async Task<(string FileName, string ContentType, byte[] Data)> ReadMimeAttachmentAsync(
        MimeEntity attachment,
        int index,
        CancellationToken cancellationToken)
    {
        switch (attachment)
        {
            case MimePart mimePart:
            {
                await using var memory = new MemoryStream();
                await mimePart.Content.DecodeToAsync(memory, cancellationToken);
                var fileName = ResolveMimeAttachmentFileName(mimePart.FileName, index, ".bin");
                var contentType = GuessContentType(fileName, mimePart.ContentType?.MimeType);
                return (fileName, contentType, memory.ToArray());
            }
            case MessagePart messagePart:
            {
                await using var memory = new MemoryStream();
                await messagePart.Message.WriteToAsync(memory, cancellationToken);
                var fileName = ResolveMimeAttachmentFileName(
                    messagePart.ContentDisposition?.FileName ?? messagePart.ContentType?.Name,
                    index,
                    ".eml");
                return (fileName, "message/rfc822", memory.ToArray());
            }
            default:
            {
                await using var memory = new MemoryStream();
                await attachment.WriteToAsync(memory, cancellationToken);
                var fileName = ResolveMimeAttachmentFileName(attachment.ContentType?.Name, index, ".bin");
                var contentType = GuessContentType(fileName, attachment.ContentType?.MimeType);
                return (fileName, contentType, memory.ToArray());
            }
        }
    }

    private static bool IsMsg(string contentType, string extension)
        => extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase);

    private static string GetEffectiveExtension(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return ".tar.gz";

        return Path.GetExtension(fileName ?? string.Empty);
    }

    private static string GuessContentType(string fileName, string? declaredContentType)
    {
        var normalizedDeclared = NormalizeContentType(declaredContentType);
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

    private static string ResolveMimeAttachmentFileName(string? fileName, int index, string defaultExtension)
    {
        var normalized = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = $"attachment-{index}{defaultExtension}";

        return EnsureExtension(normalized, defaultExtension);
    }

    private static string ResolveMsgAttachmentFileName(string? fileName, int index, string defaultExtension, string? fallbackSubject = null)
    {
        var normalized = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalized) && !string.IsNullOrWhiteSpace(fallbackSubject))
            normalized = SanitizeFileName(fallbackSubject);

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = $"attachment-{index}{defaultExtension}";

        return EnsureExtension(normalized, defaultExtension);
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        var candidate = fileName.Trim();
        if (!candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            candidate += extension;

        return candidate;
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var candidate = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            candidate = candidate.Replace(invalid, '_');

        candidate = candidate.Replace('/', '_').Replace('\\', '_');
        return candidate;
    }

    private static string? FormatMailbox(MailboxAddress? mailbox)
    {
        if (mailbox is null)
            return null;

        return FormatMailbox(mailbox.Name, mailbox.Address);
    }

    private static string? FormatMailbox(string? displayName, string? email)
    {
        var normalizedName = NormalizeInlineText(displayName ?? string.Empty);
        var normalizedEmail = NormalizeInlineText(email ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedName) && string.IsNullOrWhiteSpace(normalizedEmail))
            return null;

        if (string.IsNullOrWhiteSpace(normalizedName))
            return normalizedEmail;
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return normalizedName;

        return $"{normalizedName} <{normalizedEmail}>";
    }

    private static IReadOnlyList<string> SplitRecipients(string? recipients)
    {
        if (string.IsNullOrWhiteSpace(recipients))
            return Array.Empty<string>();

        return recipients
            .Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;

        return contentType
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0]
            .ToLowerInvariant();
    }

    private static string StripHtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var content = Regex.Replace(html, "<\\s*br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "</\\s*(p|div|li|h1|h2|h3|h4|h5|h6|tr)\\s*>", "\n", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "<[^>]+>", " ");
        content = WebUtility.HtmlDecode(content);
        return NormalizeStructuredText(content);
    }

    private static string NormalizeInlineText(string value)
        => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static string NormalizeStructuredText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').Select(line => line.TrimEnd()).ToList();
        var joined = string.Join('\n', lines);
        joined = Regex.Replace(joined, @"\n{3,}", "\n\n");
        return joined.Trim();
    }
}
