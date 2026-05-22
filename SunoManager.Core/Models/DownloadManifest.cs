using System.Text.Json.Serialization;

namespace SunoManager.Core.Models;

public class DownloadManifest
{
    [JsonPropertyName("lastSync")]
    public DateTimeOffset? LastSync { get; set; }

    [JsonPropertyName("songs")]
    public Dictionary<string, ManifestEntry> Songs { get; set; } = [];
}

public class ManifestEntry
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("playlist")]
    public string Playlist { get; set; } = "";

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = "";

    [JsonPropertyName("downloadedAt")]
    public DateTimeOffset DownloadedAt { get; set; }
}
