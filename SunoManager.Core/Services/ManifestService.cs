using System.Text.Json;
using SunoManager.Core.Models;

namespace SunoManager.Core.Services;

public class ManifestService(SunoConfig config)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private string ManifestPath => Path.Combine(config.LibraryPath, ".manifest.json");

    public async Task<DownloadManifest> LoadAsync()
    {
        if (!File.Exists(ManifestPath)) return new DownloadManifest();
        var json = await File.ReadAllTextAsync(ManifestPath);
        return JsonSerializer.Deserialize<DownloadManifest>(json) ?? new DownloadManifest();
    }

    public async Task SaveAsync(DownloadManifest manifest)
    {
        Directory.CreateDirectory(config.LibraryPath);
        manifest.LastSync = DateTimeOffset.UtcNow;
        await File.WriteAllTextAsync(ManifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
    }

    public bool IsDownloaded(DownloadManifest manifest, string clipId) =>
        manifest.Songs.ContainsKey(clipId);

    public void Record(DownloadManifest manifest, string clipId, string title,
        string playlistName, string relativePath, double? durationSeconds = null)
    {
        manifest.Songs[clipId] = new ManifestEntry
        {
            Title = title,
            Playlist = playlistName,
            RelativePath = relativePath,
            DownloadedAt = DateTimeOffset.UtcNow,
            DurationSeconds = durationSeconds
        };
    }
}
