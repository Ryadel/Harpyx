using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public record OcrModelContext(
    LlmProvider Provider,
    string ApiKey,
    string? Model);

public interface ILlmOcrService
{
    Task<string> ExtractImageTextAsync(
        string imagePath,
        string languages,
        OcrModelContext provider,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ExtractPdfTextAsync(
        string pdfPath,
        string languages,
        OcrModelContext provider,
        CancellationToken cancellationToken);
}

public class LlmOcrService : ILlmOcrService
{
    private const string OpenAiDefaultModel = "gpt-4o";
    private const string GoogleDefaultModel = "gemini-1.5-pro";
    private const string ClaudeDefaultModel = "claude-sonnet-4-5-20250929";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmOcrService> _logger;

    public LlmOcrService(
        IHttpClientFactory httpClientFactory,
        ILogger<LlmOcrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> ExtractImageTextAsync(
        string imagePath,
        string languages,
        OcrModelContext provider,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var base64 = Convert.ToBase64String(bytes);
        var mimeType = DetectImageMimeType(imagePath);
        var prompt = BuildOcrInstruction(languages);

        return provider.Provider switch
        {
            LlmProvider.OpenAI => await ExtractWithOpenAiAsync(
                base64,
                mimeType,
                prompt,
                provider.ApiKey,
                provider.Model ?? OpenAiDefaultModel,
                cancellationToken),
            LlmProvider.Google => await ExtractWithGoogleAsync(
                base64,
                mimeType,
                prompt,
                provider.ApiKey,
                provider.Model ?? GoogleDefaultModel,
                cancellationToken),
            LlmProvider.Claude => await ExtractWithClaudeAsync(
                base64,
                mimeType,
                prompt,
                provider.ApiKey,
                provider.Model ?? ClaudeDefaultModel,
                cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported OCR provider: {provider.Provider}")
        };
    }

    public async Task<IReadOnlyList<string>> ExtractPdfTextAsync(
        string pdfPath,
        string languages,
        OcrModelContext provider,
        CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"harpyx-llm-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var prefix = Path.Combine(workDir, "page");
            await RunProcessAsync(
                "pdftoppm",
                $"-png \"{pdfPath}\" \"{prefix}\"",
                cancellationToken);

            var images = Directory.GetFiles(workDir, "page-*.png")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var pages = new List<string>(images.Count);
            foreach (var image in images)
            {
                pages.Add(await ExtractImageTextAsync(image, languages, provider, cancellationToken));
            }

            return pages;
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    private async Task<string> ExtractWithOpenAiAsync(
        string base64Image,
        string mimeType,
        string prompt,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient("OpenAI");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model,
            temperature = 0,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are an OCR engine. Return only extracted text."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{mimeType};base64,{base64Image}"
                            }
                        }
                    }
                }
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI OCR API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"OpenAI OCR API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");

        return ExtractMessageText(message);
    }

    private async Task<string> ExtractWithGoogleAsync(
        string base64Image,
        string mimeType,
        string prompt,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient("Google");
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Image
                            }
                        }
                    }
                }
            }
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");
        using var response = await http.PostAsync(url, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google OCR API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Google OCR API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var parts = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts");

        var text = string.Join(
            "\n",
            parts.EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString())
                .Where(v => !string.IsNullOrWhiteSpace(v)));

        return text.Trim();
    }

    private async Task<string> ExtractWithClaudeAsync(
        string base64Image,
        string mimeType,
        string prompt,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient("Claude");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model,
            max_tokens = 4096,
            system = "You are an OCR engine. Return only extracted text.",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mimeType,
                                data = base64Image
                            }
                        }
                    }
                }
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Claude OCR API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Claude OCR API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return (text ?? string.Empty).Trim();
    }

    private static string ExtractMessageText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return (content.GetString() ?? string.Empty).Trim();

        if (content.ValueKind == JsonValueKind.Array)
        {
            var text = string.Join(
                "\n",
                content.EnumerateArray()
                    .Where(item =>
                        item.TryGetProperty("type", out var type) &&
                        type.GetString() == "text" &&
                        item.TryGetProperty("text", out _))
                    .Select(item => item.GetProperty("text").GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            return text.Trim();
        }

        return string.Empty;
    }

    private static string BuildOcrInstruction(string languages)
        => $"Extract all visible text from the image and return only plain text in reading order. Languages hint: {languages}.";

    private static string DetectImageMimeType(string imagePath)
    {
        var extension = Path.GetExtension(imagePath);
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };
    }

    private static async Task RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Unable to start process: {fileName}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"OCR dependency '{fileName}' is not available. Install required OCR binaries on the worker host.",
                ex);
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        _ = await stdOutTask;
        var stdErr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            var message = new StringBuilder();
            message.Append($"Process '{fileName}' failed with exit code {process.ExitCode}.");
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                message.Append(' ');
                message.Append(stdErr.Trim());
            }

            throw new InvalidOperationException(message.ToString());
        }
    }
}
