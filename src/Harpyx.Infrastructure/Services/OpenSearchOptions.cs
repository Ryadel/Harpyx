namespace Harpyx.Infrastructure.Services;

public class OpenSearchOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://localhost:9200";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool AllowInsecureTls { get; set; } = true;
    public string IndexName { get; set; } = "harpyx_chunks_v1";
    public string IndexAlias { get; set; } = "harpyx_chunks_current";
    public int EmbeddingDimensions { get; set; } = 1536;
    public int RequestTimeoutSeconds { get; set; } = 30;
}
