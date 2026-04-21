using System.Diagnostics;
using System.Text;

namespace Harpyx.Infrastructure.Services;

public interface ICliOcrService
{
    Task<string> ExtractImageTextAsync(string imagePath, string languages, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ExtractPdfTextAsync(string pdfPath, string languages, CancellationToken cancellationToken);
}

public class CliOcrService : ICliOcrService
{
    public async Task<string> ExtractImageTextAsync(string imagePath, string languages, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            "tesseract",
            $"\"{imagePath}\" stdout -l {languages} --dpi 300",
            cancellationToken);

        return result.Trim();
    }

    public async Task<IReadOnlyList<string>> ExtractPdfTextAsync(string pdfPath, string languages, CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"harpyx-ocr-{Guid.NewGuid():N}");
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
                pages.Add(await ExtractImageTextAsync(image, languages, cancellationToken));
            }

            return pages;
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    private static async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
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

        var stdOut = await stdOutTask;
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

        return stdOut;
    }
}
