using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Harpyx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public class UrlFetcherService : IUrlFetcher
{
    private static readonly Regex TitleRegex = new(
        @"<title[^>]*>\s*(.+?)\s*</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UrlFetchOptions _options;
    private readonly ILogger<UrlFetcherService> _logger;

    public UrlFetcherService(
        IHttpClientFactory httpClientFactory,
        UrlFetchOptions options,
        ILogger<UrlFetcherService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<UrlFetchResult> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var uri = new Uri(url);

        if (!_options.AllowHttp && uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("HTTP URLs are not allowed. Only HTTPS is supported.");

        await ValidateNotPrivateAddressAsync(uri, cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var httpClient = _httpClientFactory.CreateClient("UrlFetcher");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Harpyx/1.0");

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > _options.MaxContentBytes)
            throw new InvalidOperationException(
                $"Content too large ({contentLength} bytes). Maximum allowed: {_options.MaxContentBytes} bytes.");

        var stream = new MemoryStream();
        try
        {
            await CopyBoundedAsync(response.Content, stream, _options.MaxContentBytes, cts.Token);
            stream.Position = 0;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;

        string? title = null;
        if (contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
        {
            title = ExtractHtmlTitle(stream);
            stream.Position = 0;
        }

        _logger.LogInformation("Fetched URL {Url}: {ContentType}, {Length} bytes, title={Title}",
            url, contentType, stream.Length, title ?? "(none)");

        return new UrlFetchResult(stream, contentType, stream.Length, title);
    }

    private static async Task CopyBoundedAsync(HttpContent content, MemoryStream destination, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        await using var source = await content.ReadAsStreamAsync(ct);
        long totalRead = 0;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, ct)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxBytes)
                throw new InvalidOperationException(
                    $"Content exceeds maximum allowed size of {maxBytes} bytes.");

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
        }
    }

    private static async Task ValidateNotPrivateAddressAsync(Uri uri, CancellationToken ct)
    {
        var host = uri.DnsSafeHost;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Access to host '{host}' is not allowed.");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (SocketException)
        {
            throw new InvalidOperationException($"Could not resolve host '{host}'.");
        }

        if (addresses.Length == 0)
            throw new InvalidOperationException($"Could not resolve host '{host}'.");

        foreach (var addr in addresses)
        {
            if (IsPrivateOrReserved(addr))
                throw new InvalidOperationException(
                    $"Access to host '{host}' is not allowed (resolves to private/reserved address).");
        }
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return true;

            // fc00::/7 (unique local)
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
                return true;

            return false;
        }

        var octets = address.GetAddressBytes();
        if (octets.Length != 4)
            return false;

        // 10.0.0.0/8
        if (octets[0] == 10)
            return true;

        // 172.16.0.0/12
        if (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
            return true;

        // 192.168.0.0/16
        if (octets[0] == 192 && octets[1] == 168)
            return true;

        // 127.0.0.0/8 (loopback, redundant with IsLoopback but explicit)
        if (octets[0] == 127)
            return true;

        // 169.254.0.0/16 (link-local, includes metadata endpoint 169.254.169.254)
        if (octets[0] == 169 && octets[1] == 254)
            return true;

        // 0.0.0.0/8
        if (octets[0] == 0)
            return true;

        return false;
    }

    private static string? ExtractHtmlTitle(Stream stream)
    {
        const int peekSize = 8192;
        var buffer = new byte[peekSize];
        var bytesRead = stream.Read(buffer, 0, peekSize);
        if (bytesRead == 0)
            return null;

        var snippet = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        var match = TitleRegex.Match(snippet);
        if (!match.Success)
            return null;

        var title = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        return string.IsNullOrWhiteSpace(title) ? null : title;
    }
}
