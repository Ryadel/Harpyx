using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;
using Microsoft.Extensions.Logging;
using RtfPipe;
using UglyToad.PdfPig;
using YamlDotNet.Serialization;

namespace Harpyx.Infrastructure.Services;

public interface IDocumentTextExtractionService
{
    Task<IReadOnlyList<ExtractedPageText>> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        bool allowOcr,
        OcrModelContext? OcrModel,
        CancellationToken cancellationToken);
}

public record ExtractedPageText(int PageNumber, string Text, string SourceType, double? OcrConfidence);

public class DocumentTextExtractionService : IDocumentTextExtractionService
{
    private static readonly HashSet<string> TextContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/csv",
        "text/markdown",
        "application/json",
        "application/xml",
        "text/xml"
    };

    private static readonly HashSet<string> YamlContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/yaml",
        "application/x-yaml",
        "text/yaml",
        "text/x-yaml"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".rar",
        ".7z",
        ".tgz",
        ".tar.gz",
        ".gz"
    };

    private readonly RagOptions _options;
    private readonly ILlmOcrService _llmOcr;
    private readonly ICliOcrService _ocr;
    private readonly ILogger<DocumentTextExtractionService> _logger;

    public DocumentTextExtractionService(
        RagOptions options,
        ILlmOcrService llmOcr,
        ICliOcrService ocr,
        ILogger<DocumentTextExtractionService> logger)
    {
        _options = options;
        _llmOcr = llmOcr;
        _ocr = ocr;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtractedPageText>> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        bool allowOcr,
        OcrModelContext? OcrModel,
        CancellationToken cancellationToken)
    {
        var extension = GetEffectiveExtension(fileName);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using (var file = File.Create(tempPath))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            if (IsPdf(contentType, extension))
                return await ExtractFromPdfAsync(tempPath, allowOcr, OcrModel, cancellationToken);

            if (IsImage(contentType, extension))
                return await ExtractFromImageAsync(tempPath, allowOcr, OcrModel, cancellationToken);

            if (IsWordDocument(contentType, extension))
                return await ExtractFromDocxAsync(tempPath, cancellationToken);

            if (IsSpreadsheet(contentType, extension))
                return await ExtractFromXlsxAsync(tempPath, cancellationToken);

            if (IsPresentation(contentType, extension))
                return await ExtractFromPptxAsync(tempPath, cancellationToken);

            if (IsOpenDocument(contentType, extension))
                return await ExtractFromOpenDocumentAsync(tempPath, extension, cancellationToken);

            if (IsEmail(contentType, extension))
                return await ExtractFromEmailAsync(tempPath, contentType, cancellationToken);

            if (IsRtf(contentType, extension))
                return await ExtractFromRtfAsync(tempPath, cancellationToken);

            if (IsEpub(contentType, extension))
                return await ExtractFromEpubAsync(tempPath, cancellationToken);

            if (IsYaml(contentType, extension))
                return await ExtractFromYamlAsync(tempPath, cancellationToken);

            if (IsHtml(contentType, extension))
                return await ExtractFromHtmlFileAsync(tempPath, cancellationToken);

            if (IsText(contentType, extension))
                return await ExtractFromTextFileAsync(tempPath, cancellationToken);

            if (IsArchive(contentType, extension))
                throw new NotSupportedException("Archive recursive extraction is not enabled yet.");

            throw new NotSupportedException($"Unsupported content type for RAG extraction: {contentType}");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private async Task<IReadOnlyList<ExtractedPageText>> ExtractFromPdfAsync(
        string pdfPath,
        bool allowOcr,
        OcrModelContext? OcrModel,
        CancellationToken cancellationToken)
    {
        var pages = new List<ExtractedPageText>();
        using (var pdf = PdfDocument.Open(pdfPath))
        {
            foreach (var page in pdf.GetPages())
            {
                pages.Add(new ExtractedPageText(
                    page.Number,
                    NormalizeInlineText(page.Text),
                    "text",
                    null));
            }
        }

        var textChars = pages.Sum(p => p.Text.Count(ch => !char.IsWhiteSpace(ch)));
        if (textChars >= _options.PdfTextMinCharsBeforeOcr)
        {
            var nonEmpty = pages.Where(p => !string.IsNullOrWhiteSpace(p.Text)).ToList();
            if (nonEmpty.Count > 0)
                return nonEmpty;
        }

        if (!allowOcr)
        {
            var nonEmpty = pages.Where(p => !string.IsNullOrWhiteSpace(p.Text)).ToList();
            if (nonEmpty.Count > 0)
                return nonEmpty;

            throw new InvalidOperationException("OCR is disabled for this instance and PDF has no extractable text.");
        }

        _logger.LogInformation("PDF text extraction was insufficient ({Chars} chars), running OCR.", textChars);
        var extracted = await ExtractPdfByOcrAsync(pdfPath, OcrModel, cancellationToken);

        if (extracted.Count == 0)
            throw new InvalidOperationException("No text extracted from PDF, including OCR fallback.");

        return extracted;
    }

    private async Task<IReadOnlyList<ExtractedPageText>> ExtractFromImageAsync(
        string imagePath,
        bool allowOcr,
        OcrModelContext? OcrModel,
        CancellationToken cancellationToken)
    {
        if (!allowOcr)
            throw new InvalidOperationException("OCR is disabled for this instance and image text extraction is unavailable.");

        string text;
        if (OcrModel is not null)
        {
            try
            {
                _logger.LogInformation("Using LLM OCR provider {Provider} for image extraction.", OcrModel.Provider);
                text = await _llmOcr.ExtractImageTextAsync(imagePath, _options.OcrLanguages, OcrModel, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM OCR provider failed for image extraction. Falling back to local OCR.");
                text = await _ocr.ExtractImageTextAsync(imagePath, _options.OcrLanguages, cancellationToken);
            }
        }
        else
        {
            text = await _ocr.ExtractImageTextAsync(imagePath, _options.OcrLanguages, cancellationToken);
        }

        var normalized = NormalizeInlineText(text);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("No text extracted from image OCR.");

        return [new ExtractedPageText(1, normalized, "ocr", null)];
    }

    private async Task<IReadOnlyList<ExtractedPageText>> ExtractPdfByOcrAsync(
        string pdfPath,
        OcrModelContext? OcrModel,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ocrPages;
        if (OcrModel is not null)
        {
            try
            {
                _logger.LogInformation("Using LLM OCR provider {Provider} for PDF extraction.", OcrModel.Provider);
                ocrPages = await _llmOcr.ExtractPdfTextAsync(pdfPath, _options.OcrLanguages, OcrModel, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM OCR provider failed for PDF extraction. Falling back to local OCR.");
                ocrPages = await _ocr.ExtractPdfTextAsync(pdfPath, _options.OcrLanguages, cancellationToken);
            }
        }
        else
        {
            ocrPages = await _ocr.ExtractPdfTextAsync(pdfPath, _options.OcrLanguages, cancellationToken);
        }

        return ocrPages
            .Select((text, idx) => new ExtractedPageText(idx + 1, NormalizeInlineText(text), "ocr", null))
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .ToList();
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromTextFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        var normalized = NormalizeStructuredText(text);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("No text extracted from text document.");

        return [new ExtractedPageText(1, normalized, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromHtmlFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        var text = StripHtmlToText(html);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No text extracted from HTML document.");

        return [new ExtractedPageText(1, text, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromDocxAsync(string filePath, CancellationToken cancellationToken)
    {
        await Task.Yield();

        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body
                   ?? throw new InvalidOperationException("DOCX body not found.");

        var lines = new List<string>();
        foreach (var child in body.ChildElements)
        {
            if (child is DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
            {
                var paragraphText = NormalizeInlineText(string.Concat(paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text)));
                if (string.IsNullOrWhiteSpace(paragraphText))
                    continue;

                lines.Add(IsHeading(paragraph) ? $"## {paragraphText}" : paragraphText);
                continue;
            }

            if (child is DocumentFormat.OpenXml.Wordprocessing.Table table)
            {
                lines.Add("[Table]");
                foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    var cells = row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                        .Select(cell => NormalizeInlineText(string.Concat(cell.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text))))
                        .ToList();
                    if (cells.Count > 0)
                        lines.Add(string.Join('\t', cells));
                }

                lines.Add("[/Table]");
            }
        }

        var normalized = NormalizeStructuredText(string.Join('\n', lines));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("No text extracted from DOCX.");

        return [new ExtractedPageText(1, normalized, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromXlsxAsync(string filePath, CancellationToken cancellationToken)
    {
        await Task.Yield();

        using var spreadsheet = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = spreadsheet.WorkbookPart ?? throw new InvalidOperationException("XLSX workbook part not found.");
        var sheets = workbookPart.Workbook.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList() ?? [];
        if (sheets.Count == 0)
            throw new InvalidOperationException("No worksheets found in XLSX.");

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var pages = new List<ExtractedPageText>();
        var pageNumber = 1;

        foreach (var sheet in sheets)
        {
            if (string.IsNullOrWhiteSpace(sheet.Id?.Value))
                continue;

            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
            var sheetData = worksheetPart.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.SheetData>();
            var sb = new StringBuilder();
            sb.AppendLine($"## Sheet: {sheet.Name?.Value ?? "Sheet"}");

            var maxCells = 20000;
            var cellCount = 0;
            var truncated = false;

            if (sheetData is not null)
            {
                foreach (var row in sheetData.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>())
                {
                    var values = new List<string>();
                    foreach (var cell in row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                    {
                        if (cellCount >= maxCells)
                        {
                            truncated = true;
                            break;
                        }

                        values.Add(ReadSpreadsheetCellValue(cell, sharedStrings));
                        cellCount++;
                    }

                    if (values.Count > 0)
                        sb.AppendLine(string.Join('\t', values));

                    if (truncated)
                        break;
                }
            }

            if (truncated)
                sb.AppendLine("[Warning] Sheet truncated because extraction cell limit was exceeded.");

            var text = NormalizeStructuredText(sb.ToString());
            if (!string.IsNullOrWhiteSpace(text))
                pages.Add(new ExtractedPageText(pageNumber++, text, "text", null));
        }

        if (pages.Count == 0)
            throw new InvalidOperationException("No text extracted from XLSX.");

        return pages;
    }

    private static bool IsHeading(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
    {
        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return !string.IsNullOrWhiteSpace(style) && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadSpreadsheetCellValue(
        DocumentFormat.OpenXml.Spreadsheet.Cell cell,
        DocumentFormat.OpenXml.Spreadsheet.SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString)
        {
            if (int.TryParse(cell.CellValue?.InnerText, out var idx) &&
                sharedStrings is not null &&
                idx >= 0 &&
                idx < sharedStrings.Count())
            {
                return NormalizeInlineText(sharedStrings.ElementAt(idx).InnerText);
            }

            return string.Empty;
        }

        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.Boolean)
            return cell.CellValue?.InnerText == "1" ? "TRUE" : "FALSE";

        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString)
            return NormalizeInlineText(cell.InlineString?.InnerText ?? string.Empty);

        return NormalizeInlineText(cell.CellValue?.InnerText ?? cell.InnerText);
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromPptxAsync(string filePath, CancellationToken cancellationToken)
    {
        await Task.Yield();

        using var presentation = PresentationDocument.Open(filePath, false);
        var presentationPart = presentation.PresentationPart ?? throw new InvalidOperationException("PPTX presentation part not found.");
        var slideIdList = presentationPart.Presentation?.SlideIdList;
        var slideIds = slideIdList is null
            ? []
            : slideIdList.ChildElements.ToList();
        if (slideIds.Count == 0)
            throw new InvalidOperationException("No slides found in PPTX.");

        var pages = new List<ExtractedPageText>();
        for (var i = 0; i < slideIds.Count; i++)
        {
            if (slideIds[i] is not DocumentFormat.OpenXml.Presentation.SlideId slideId || string.IsNullOrWhiteSpace(slideId.RelationshipId))
                continue;

            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
            var slideText = slidePart.Slide
                .Descendants<DrawingText>()
                .Select(t => NormalizeInlineText(t.Text))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            var notesText = slidePart.NotesSlidePart?.NotesSlide?
                .Descendants<DrawingText>()
                .Select(t => NormalizeInlineText(t.Text))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList() ?? [];

            var sb = new StringBuilder();
            var title = slideText.FirstOrDefault() ?? $"Slide {i + 1}";
            sb.AppendLine($"## Slide {i + 1}: {title}");

            if (slideText.Count > 0)
            {
                foreach (var bullet in slideText.Skip(1))
                {
                    sb.AppendLine($"- {bullet}");
                }
            }
            else
            {
                sb.AppendLine("[Warning] Image-only slide; OCR on slide images is not enabled in this pipeline.");
            }

            if (notesText.Count > 0)
            {
                sb.AppendLine("Notes:");
                foreach (var note in notesText)
                {
                    sb.AppendLine($"- {note}");
                }
            }

            var pageText = NormalizeStructuredText(sb.ToString());
            if (!string.IsNullOrWhiteSpace(pageText))
                pages.Add(new ExtractedPageText(i + 1, pageText, "text", null));
        }

        if (pages.Count == 0)
            throw new InvalidOperationException("No text extracted from PPTX.");

        return pages;
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromOpenDocumentAsync(string filePath, string extension, CancellationToken cancellationToken)
    {
        await Task.Yield();

        using var archive = ZipFile.OpenRead(filePath);
        var contentEntry = archive.GetEntry("content.xml")
            ?? throw new InvalidOperationException($"{extension} content.xml entry not found.");

        string xml;
        await using (var stream = contentEntry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8, true))
        {
            xml = await reader.ReadToEndAsync(cancellationToken);
        }

        var document = XDocument.Parse(xml);
        var lines = new List<string>();

        foreach (var row in document.Descendants().Where(e => e.Name.LocalName == "table-row"))
        {
            var cells = row.Elements().Where(e => e.Name.LocalName == "table-cell")
                .Select(cell => NormalizeInlineText(string.Concat(cell.DescendantNodes().OfType<XText>().Select(t => t.Value))))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (cells.Count > 0)
                lines.Add(string.Join('\t', cells));
        }

        foreach (var paragraph in document.Descendants().Where(e => e.Name.LocalName is "h" or "p"))
        {
            var text = NormalizeInlineText(string.Concat(paragraph.DescendantNodes().OfType<XText>().Select(t => t.Value)));
            if (string.IsNullOrWhiteSpace(text))
                continue;

            lines.Add(paragraph.Name.LocalName == "h" ? $"## {text}" : text);
        }

        var normalized = NormalizeStructuredText(string.Join('\n', lines));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException($"No text extracted from {extension}.");

        return [new ExtractedPageText(1, normalized, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromEmailAsync(
        string filePath,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var message = await NormalizedEmailMessageParser.ParseAsync(
            Path.GetFileName(filePath),
            contentType,
            stream,
            cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Subject: {message.Subject}");
        sb.AppendLine($"From: {message.From ?? string.Empty}");
        sb.AppendLine($"To: {string.Join(", ", message.To)}");
        if (message.Cc.Count > 0)
            sb.AppendLine($"Cc: {string.Join(", ", message.Cc)}");
        if (message.SentOn is DateTimeOffset sentOn)
            sb.AppendLine($"Date: {sentOn.UtcDateTime:O}");

        var body = message.Body;

        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.AppendLine(body);
        }

        var attachmentNames = message.Attachments
            .Select(a => a.FileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (attachmentNames.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Attachments:");
            foreach (var name in attachmentNames)
                sb.AppendLine($"- {name}");
        }

        var normalized = NormalizeStructuredText(sb.ToString());
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("No text extracted from email document.");

        return [new ExtractedPageText(1, normalized, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromRtfAsync(string filePath, CancellationToken cancellationToken)
    {
        var rtf = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        if (string.IsNullOrWhiteSpace(rtf))
            throw new InvalidOperationException("No text extracted from RTF.");

        var html = Rtf.ToHtml(rtf);
        var text = StripHtmlToText(html);
        var normalized = NormalizeStructuredText(text);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("No text extracted from RTF.");

        return [new ExtractedPageText(1, normalized, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromYamlAsync(string filePath, CancellationToken cancellationToken)
    {
        var yaml = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        if (string.IsNullOrWhiteSpace(yaml))
            throw new InvalidOperationException("No text extracted from YAML.");

        string stable;
        try
        {
            var deserializer = new DeserializerBuilder().Build();
            var serializer = new SerializerBuilder().Build();
            var model = deserializer.Deserialize<object>(yaml);
            stable = serializer.Serialize(model);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Invalid YAML content.", ex);
        }

        var normalized = NormalizeStructuredText(stable);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("No text extracted from YAML.");

        return [new ExtractedPageText(1, normalized, "text", null)];
    }

    private static async Task<IReadOnlyList<ExtractedPageText>> ExtractFromEpubAsync(string filePath, CancellationToken cancellationToken)
    {
        await Task.Yield();

        using var archive = ZipFile.OpenRead(filePath);
        var opfPath = ResolveEpubOpfPath(archive);
        var chapterEntries = new List<(string EntryPath, string Title)>();

        if (!string.IsNullOrWhiteSpace(opfPath))
        {
            var opfEntry = archive.GetEntry(opfPath);
            if (opfEntry is not null)
            {
                string opfXml;
                await using (var opfStream = opfEntry.Open())
                using (var reader = new StreamReader(opfStream, Encoding.UTF8, true))
                {
                    opfXml = await reader.ReadToEndAsync(cancellationToken);
                }

                var opf = XDocument.Parse(opfXml);
                var manifest = opf.Descendants().Where(x => x.Name.LocalName == "item")
                    .Select(x => new
                    {
                        Id = x.Attribute("id")?.Value,
                        Href = x.Attribute("href")?.Value,
                        MediaType = x.Attribute("media-type")?.Value
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Href))
                    .ToDictionary(x => x.Id!, x => x, StringComparer.OrdinalIgnoreCase);

                var baseDir = GetDirectory(opfPath);
                var orderedIds = opf.Descendants().Where(x => x.Name.LocalName == "itemref")
                    .Select(x => x.Attribute("idref")?.Value)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .ToList();

                foreach (var id in orderedIds)
                {
                    if (!manifest.TryGetValue(id, out var item))
                        continue;

                    if (!string.IsNullOrWhiteSpace(item.MediaType) &&
                        !item.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                        !item.MediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var entryPath = ResolveRelativeZipPath(baseDir, item.Href!);
                    chapterEntries.Add((entryPath, Path.GetFileNameWithoutExtension(item.Href!)));
                }
            }
        }

        if (chapterEntries.Count == 0)
        {
            chapterEntries = archive.Entries
                .Where(e => e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(e => (e.FullName, Path.GetFileNameWithoutExtension(e.Name)))
                .ToList();
        }

        var pages = new List<ExtractedPageText>();
        var page = 1;
        foreach (var chapter in chapterEntries)
        {
            var entry = archive.GetEntry(chapter.EntryPath);
            if (entry is null)
                continue;

            string html;
            await using (var chapterStream = entry.Open())
            using (var reader = new StreamReader(chapterStream, Encoding.UTF8, true))
            {
                html = await reader.ReadToEndAsync(cancellationToken);
            }

            var chapterText = NormalizeStructuredText(StripHtmlToText(html));
            if (string.IsNullOrWhiteSpace(chapterText))
                continue;

            var content = NormalizeStructuredText($"## Chapter: {NormalizeInlineText(chapter.Title)}\n{chapterText}");
            pages.Add(new ExtractedPageText(page++, content, "text", null));
        }

        if (pages.Count == 0)
            throw new InvalidOperationException("No text extracted from EPUB.");

        return pages;
    }

    private static bool IsPdf(string contentType, string extension)
        => contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsImage(string contentType, string extension)
        => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

    private static bool IsWordDocument(string contentType, string extension)
        => extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase);

    private static bool IsSpreadsheet(string contentType, string extension)
        => extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase);

    private static bool IsPresentation(string contentType, string extension)
        => extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.openxmlformats-officedocument.presentationml.presentation", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenDocument(string contentType, string extension)
        => extension.Equals(".odt", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".ods", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".odp", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.oasis.opendocument.text", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.oasis.opendocument.spreadsheet", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.oasis.opendocument.presentation", StringComparison.OrdinalIgnoreCase);

    private static bool IsEmail(string contentType, string extension)
        => extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase);

    private static bool IsRtf(string contentType, string extension)
        => extension.Equals(".rtf", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/rtf", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("text/rtf", StringComparison.OrdinalIgnoreCase);

    private static bool IsEpub(string contentType, string extension)
        => extension.Equals(".epub", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/epub+zip", StringComparison.OrdinalIgnoreCase);

    private static bool IsYaml(string contentType, string extension)
        => extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
           || YamlContentTypes.Contains(contentType);

    private static bool IsHtml(string contentType, string extension)
        => contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);

    private static bool IsText(string contentType, string extension)
        => TextContentTypes.Contains(contentType)
           || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsArchive(string contentType, string extension)
        => ArchiveExtensions.Contains(extension)
           || contentType.Equals("application/zip", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/x-zip-compressed", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/vnd.rar", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/x-rar-compressed", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/x-7z-compressed", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/gzip", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/x-gzip", StringComparison.OrdinalIgnoreCase)
           || contentType.Equals("application/x-tar", StringComparison.OrdinalIgnoreCase);

    private static string GetEffectiveExtension(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return ".tar.gz";

        return Path.GetExtension(fileName);
    }

    private static string ResolveEpubOpfPath(ZipArchive archive)
    {
        var containerEntry = archive.GetEntry("META-INF/container.xml");
        if (containerEntry is null)
            return archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase))?.FullName ?? string.Empty;

        using var stream = containerEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var xml = reader.ReadToEnd();
        var doc = XDocument.Parse(xml);
        var path = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "rootfile")?.Attribute("full-path")?.Value;
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        return archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase))?.FullName ?? string.Empty;
    }

    private static string ResolveRelativeZipPath(string baseDir, string relativePath)
    {
        var normalizedBase = baseDir.Replace('\\', '/').Trim('/');
        var origin = string.IsNullOrWhiteSpace(normalizedBase)
            ? new Uri("http://local/")
            : new Uri($"http://local/{normalizedBase}/");
        var resolved = new Uri(origin, relativePath.Replace('\\', '/'));
        return resolved.AbsolutePath.TrimStart('/');
    }

    private static string GetDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        var idx = normalized.LastIndexOf('/');
        return idx < 0 ? string.Empty : normalized[..idx];
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
