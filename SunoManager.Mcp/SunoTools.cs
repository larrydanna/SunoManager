using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SunoManager.Core;
using SunoManager.Core.Services;

namespace SunoManager.Mcp;

[McpServerToolType]
public class SunoTools(SunoApiClient api, DownloadService downloader,
    ManifestService manifestService, ExportService exporter, SunoConfig config)
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private string? EnsureActiveToken()
    {
        if (!config.IsTokenExpired()) return null;

        var stored = TokenStore.TryRead();
        if (string.IsNullOrWhiteSpace(stored))
            return $"Authentication token unavailable. No stored token found at {TokenStore.FilePath}.";

        stored = stored.Trim();
        var (valid, _, error) = TokenStore.Validate(stored);
        if (!valid)
            return $"Authentication token unavailable: stored token is invalid ({error}).";

        var normalized = stored.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? stored : "Bearer " + stored;

        config.AuthToken = normalized;
        TokenStore.Save(normalized);
        return null;
    }

    [McpServerTool, Description("Activate a fresh Suno auth token for this running server -- no restart needed. Preferred usage: run 'suno token' in a terminal (it writes the shared token.json), then call this with NO argument to reload it from disk. You may also pass the full 'Bearer eyJ...' value directly, but reloading from disk avoids copying a long secret through the model, which can corrupt it.")]
    public string set_token(
        [Description("Optional. The full 'Bearer eyJ...' value. If omitted, the token is reloaded from the shared token.json file written by 'suno token'.")]
        string? token = null)
    {
        var fromDisk = string.IsNullOrWhiteSpace(token);
        if (fromDisk)
        {
            token = TokenStore.TryRead();
            if (string.IsNullOrWhiteSpace(token))
                return $"No token supplied and no stored token found at {TokenStore.FilePath}.";
        }

        token = token!.Trim();
        var (valid, expiry, error) = TokenStore.Validate(token);
        if (!valid)
            return $"Token rejected: {error}";

        var normalized = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token : "Bearer " + token;

        config.AuthToken = normalized;   // live singleton -- every service sees it immediately
        TokenStore.Save(normalized);     // persist to token.json for the CLI and other hosts

        var mins = (int)(expiry - DateTimeOffset.UtcNow).TotalMinutes;
        var src = fromDisk ? "reloaded from token.json" : "accepted";
        return $"Token {src} and active. Valid until {expiry.LocalDateTime:ddd MMM d, h:mm tt} "
             + $"(about {mins} minutes from now).";
    }

    [McpServerTool, Description("List all Suno playlists with name, ID, and song count.")]
    public async Task<string> list_playlists()
    {
        var tokenError = EnsureActiveToken();
        if (tokenError is not null) return tokenError;

        var playlists = await api.GetAllPlaylistsAsync();
        var summary = playlists.Select(p => new
        {
            p.Id,
            p.Name,
            Songs = p.TotalClips
        });
        return JsonSerializer.Serialize(summary, Pretty);
    }

    [McpServerTool, Description("Show library sync status: total songs downloaded, last sync time, and per-playlist counts.")]
    public async Task<string> get_library_status()
    {
        var manifest = await manifestService.LoadAsync();
        var byPlaylist = manifest.Songs.Values
            .GroupBy(e => e.Playlist)
            .Select(g => new { Playlist = g.Key, Count = g.Count() })
            .OrderBy(x => x.Playlist);

        var status = new
        {
            TotalDownloaded = manifest.Songs.Count,
            LastSync = manifest.LastSync?.ToString("yyyy-MM-dd HH:mm") ?? "never",
            LibraryPath = config.LibraryPath,
            ByPlaylist = byPlaylist
        };
        return JsonSerializer.Serialize(status, Pretty);
    }

    [McpServerTool, Description("List songs in the local library, optionally filtered by playlist name.")]
    public async Task<string> list_songs(
        [Description("Optional playlist name to filter by")] string? playlist = null)
    {
        var manifest = await manifestService.LoadAsync();
        var songs = manifest.Songs.Select(kvp => new
        {
            Id = kvp.Key,
            kvp.Value.Title,
            kvp.Value.Playlist,
            kvp.Value.RelativePath,
            Downloaded = kvp.Value.DownloadedAt.ToString("yyyy-MM-dd")
        });

        if (!string.IsNullOrWhiteSpace(playlist))
            songs = songs.Where(s => s.Playlist.Equals(playlist, StringComparison.OrdinalIgnoreCase));

        return JsonSerializer.Serialize(songs.OrderBy(s => s.Playlist).ThenBy(s => s.Title), Pretty);
    }

    [McpServerTool, Description("Sync (download) a specific Suno playlist by name. Downloads only songs not already in the library.")]
    public async Task<string> sync_playlist(
        [Description("The exact playlist name to sync")] string playlistName)
    {
        var tokenError = EnsureActiveToken();
        if (tokenError is not null) return tokenError;

        var summaries = await api.GetAllPlaylistsAsync();
        var summary = summaries.FirstOrDefault(p =>
            p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

        if (summary is null)
            return $"Playlist not found: {playlistName}";

        var playlist = await api.GetPlaylistAsync(summary.Id);
        if (playlist is null) return $"Could not fetch clips for: {playlistName}";

        var messages = new List<string>();
        var progress = new Progress<string>(m => messages.Add(m));
        var result = await downloader.SyncPlaylistAsync(playlist, progress);

        messages.Add($"Done -- {result.Downloaded} downloaded, {result.Skipped} skipped, {result.Failed} failed");
        return string.Join("\n", messages);
    }

    [McpServerTool, Description("Sync all Suno playlists. Downloads only songs not already in the library.")]
    public async Task<string> sync_all()
    {
        var tokenError = EnsureActiveToken();
        if (tokenError is not null) return tokenError;

        var summaries = await api.GetAllPlaylistsAsync();
        if (summaries.Count == 0) return "No playlists found.";

        var lines = new List<string>();
        foreach (var summary in summaries)
        {
            lines.Add($"Syncing: {summary.Name} ({summary.DisplayCount} songs)");
            var playlist = await api.GetPlaylistAsync(summary.Id);
            if (playlist is null) { lines.Add("  => Could not fetch clips, skipping."); continue; }
            var progress = new Progress<string>(m => lines.Add(m));
            var result = await downloader.SyncPlaylistAsync(playlist, progress);
            lines.Add($"  => {result.Downloaded} downloaded, {result.Skipped} skipped, {result.Failed} failed");
        }
        return string.Join("\n", lines);
    }

    [McpServerTool, Description("Export the local music library to the configured USB drive path.")]
    public string export_to_usb(
        [Description("Set to true to preview what would be copied without copying")] bool dryRun = false)
    {
        if (string.IsNullOrWhiteSpace(config.UsbPath))
            return "UsbPath is not configured in appsettings.json.";

        var lines = new List<string> { dryRun ? $"[dry-run] USB path: {config.UsbPath}" : $"Exporting to: {config.UsbPath}" };
        var progress = new Progress<string>(m => lines.Add(m));
        var result = exporter.Export(dryRun, progress);

        if (result.Error is not null)
            lines.Add($"ERROR: {result.Error}");
        else
            lines.Add($"Done -- {result.Copied} copied, {result.Unchanged} unchanged");

        return string.Join("\n", lines);
    }
}
