using System.Text;
using SunoManager.Core.Models;

namespace SunoManager.Core.Services;

public class DownloadService(HttpClient http, SunoApiClient api, ManifestService manifest, SunoConfig config)
{
    // cdn2.suno.ai sits behind Cloudflare and returns 403 to requests with no
    // User-Agent, so every outbound download request carries a browser UA.
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36";

    public async Task<SyncResult> SyncPlaylistAsync(Playlist playlist,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var result = new SyncResult { PlaylistName = playlist.Name };
        var man = await manifest.LoadAsync();

        var playlistDir = Path.Combine(config.LibraryPath, playlist.SafeName);
        Directory.CreateDirectory(playlistDir);

        // The playlist's own image -> cover.jpg in the folder.
        await TryDownloadPlaylistCoverAsync(playlist, playlistDir, progress, ct);

        // Ordered (song, file) pairs, used to write the .m3u8 once the loop ends.
        var ordered = new List<(Song song, string filePath)>();
        int trackNo = 0;

        foreach (var entry in playlist.Clips)
        {
            if (entry.Clip is not { } song) continue;
            ct.ThrowIfCancellationRequested();
            trackNo++;

            if (manifest.IsDownloaded(man, song.Id))
            {
                // Already downloaded — reuse the recorded path for the playlist file.
                var existing = Path.Combine(config.LibraryPath, man.Songs[song.Id].RelativePath);
                ordered.Add((song, existing));
                result.Skipped++;
                continue;
            }

            try
            {
                progress?.Report($"  Downloading: {song.Title}");

                var downloadUrl = await api.GetDownloadUrlAsync(song.Id, config.Format);
                if (downloadUrl is null) { result.Failed++; continue; }

                var filePath = BuildUniquePath(playlistDir, song.SafeTitle, config.Format);
                await DownloadFileAsync(downloadUrl, filePath, ct);
                await api.LogDownloadAsync(song.Id);

                // Art: one download, reused for the sidecar .jpeg and the embedded tag.
                var artBytes = config.IncludeCoverArt
                    ? await TryGetBytesAsync(song.BestArtUrl, ct, progress, $"art for {song.Title}")
                    : null;
                if (artBytes is not null)
                    await File.WriteAllBytesAsync(Path.ChangeExtension(filePath, ".jpeg"), artBytes, ct);

                // Lyrics: sidecar .txt next to the audio file.
                if (!string.IsNullOrWhiteSpace(song.Lyrics))
                    await File.WriteAllTextAsync(Path.ChangeExtension(filePath, ".txt"),
                        BuildLyricsText(song), new UTF8Encoding(false), ct);

                EmbedTags(filePath, song, playlist.Name, trackNo, artBytes, progress);

                var relPath = Path.GetRelativePath(config.LibraryPath, filePath);
                manifest.Record(man, song.Id, song.Title, playlist.Name, relPath, song.DurationSeconds);
                await manifest.SaveAsync(man);

                ordered.Add((song, filePath));
                result.Downloaded++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report($"  ERROR {song.Title}: {ex.Message}");
                result.Failed++;
            }
        }

        WritePlaylistFile(playlist, playlistDir, ordered, progress);
        return result;
    }

    private static string BuildLyricsText(Song song)
    {
        var title = string.IsNullOrWhiteSpace(song.Title) ? "Untitled" : song.Title.Trim();
        return $"{title}\n\n{song.Lyrics}";
    }

    private async Task DownloadFileAsync(string url, string destPath, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(BrowserUserAgent);
        using var response = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destPath);
        await stream.CopyToAsync(file, ct);
    }

    // Fetches a URL into memory. Returns null on failure and reports why — art
    // is optional, but a silent failure should never hide the reason again.
    private async Task<byte[]?> TryGetBytesAsync(string url, CancellationToken ct,
        IProgress<string>? progress = null, string? label = null)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            using var response = await http.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            progress?.Report($"  (could not fetch {label ?? url}: {ex.Message})");
            return null;
        }
    }

    private async Task TryDownloadPlaylistCoverAsync(Playlist playlist, string dir,
        IProgress<string>? progress, CancellationToken ct)
    {
        var coverPath = Path.Combine(dir, "cover.jpg");
        if (File.Exists(coverPath) || string.IsNullOrWhiteSpace(playlist.ImageUrl)) return;
        var bytes = await TryGetBytesAsync(playlist.ImageUrl, ct, progress, "playlist cover");
        if (bytes is not null) await File.WriteAllBytesAsync(coverPath, bytes, ct);
    }

    // Writes Title, Artist, Album (= playlist), Track #, and embeds lyrics + art.
    private void EmbedTags(string filePath, Song song, string playlistName,
        int trackNo, byte[]? artBytes, IProgress<string>? progress)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var artist = string.IsNullOrWhiteSpace(config.Artist) ? "Suno" : config.Artist;

            tagFile.Tag.Title = song.Title;
            tagFile.Tag.Performers = [artist];
            tagFile.Tag.AlbumArtists = [artist];
            tagFile.Tag.Album = playlistName;
            tagFile.Tag.Track = (uint)trackNo;

            if (!string.IsNullOrWhiteSpace(song.Lyrics))
                tagFile.Tag.Lyrics = song.Lyrics;

            if (artBytes is not null)
                tagFile.Tag.Pictures =
                [
                    new TagLib.Picture(new TagLib.ByteVector(artBytes))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = "image/jpeg",
                        Description = "Cover"
                    }
                ];

            tagFile.Save();
        }
        catch (Exception ex)
        {
            progress?.Report($"  (tag embed failed for {song.Title}: {ex.Message})");
        }
    }

    // Generates (or regenerates) a master .m3u8 in the library root that lists
    // every downloaded song, sorted by playlist then title.
    public async Task WriteMasterPlaylistAsync(IProgress<string>? progress = null)
    {
        var man = await manifest.LoadAsync();
        if (man.Songs.Count == 0) return;

        var artist = string.IsNullOrWhiteSpace(config.Artist) ? "Suno" : config.Artist;
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");

        foreach (var entry in man.Songs.Values.OrderBy(e => e.Playlist).ThenBy(e => e.Title))
        {
            var secs = (int)Math.Round(entry.DurationSeconds ?? 0);
            var rel = entry.RelativePath.Replace('\\', '/');
            AppendPlaylistEntry(sb, secs, $"{artist} - {entry.Title}", rel,
                TryGetCoverPathForEntry(config.LibraryPath, rel));
        }

        var m3uPath = Path.Combine(config.LibraryPath, "All Songs.m3u8");
        await File.WriteAllTextAsync(m3uPath, sb.ToString(), new UTF8Encoding(false));
        progress?.Report($"Master playlist: {Path.GetFileName(m3uPath)} ({man.Songs.Count} tracks)");
    }

    // Generates a .m3u8 listing every song in the playlist, in playlist order.
    private void WritePlaylistFile(Playlist playlist, string playlistDir,
        List<(Song song, string filePath)> ordered, IProgress<string>? progress)
    {
        if (ordered.Count == 0) return;

        var artist = string.IsNullOrWhiteSpace(config.Artist) ? "Suno" : config.Artist;
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        var coverRel = File.Exists(Path.Combine(playlistDir, "cover.jpg")) ? "cover.jpg" : null;

        foreach (var (song, filePath) in ordered)
        {
            var secs = (int)Math.Round(song.DurationSeconds ?? 0);
            // Path relative to the playlist folder, forward slashes for portability.
            var rel = Path.GetRelativePath(playlistDir, filePath).Replace('\\', '/');
            AppendPlaylistEntry(sb, secs, $"{artist} - {song.Title}", rel, coverRel);
        }

        var m3uPath = Path.Combine(playlistDir, $"{playlist.SafeName}.m3u8");
        File.WriteAllText(m3uPath, sb.ToString(), new UTF8Encoding(false));
        progress?.Report($"  Playlist file: {Path.GetFileName(m3uPath)} ({ordered.Count} tracks)");
    }

    // Builds "<title>.<ext>"; appends " (2)", " (3)", ... only when a different
    // song in the same folder already claimed that exact name. No GUIDs, ever.
    private static string BuildUniquePath(string dir, string baseName, string ext)
    {
        var candidate = Path.Combine(dir, $"{baseName}.{ext}");
        if (!File.Exists(candidate)) return candidate;
        for (int n = 2; ; n++)
        {
            candidate = Path.Combine(dir, $"{baseName} ({n}).{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static void AppendPlaylistEntry(StringBuilder sb, int seconds,
        string displayTitle, string mediaPath, string? artworkPath)
    {
        sb.AppendLine($"#EXTINF:{seconds},{displayTitle}");
        if (!string.IsNullOrWhiteSpace(artworkPath))
        {
            sb.AppendLine($"#EXTIMG:{artworkPath}");
            sb.AppendLine($"#EXTVLCOPT:artwork-url={artworkPath}");
        }
        sb.AppendLine(mediaPath);
    }

    private static string? TryGetCoverPathForEntry(string libraryRoot, string relativeMediaPath)
    {
        var mediaDir = Path.GetDirectoryName(relativeMediaPath);
        var coverRel = string.IsNullOrWhiteSpace(mediaDir)
            ? "cover.jpg"
            : Path.Combine(mediaDir, "cover.jpg");

        return File.Exists(Path.Combine(libraryRoot, coverRel))
            ? coverRel.Replace('\\', '/')
            : null;
    }
}

public class SyncResult
{
    public string PlaylistName { get; set; } = "";
    public int Downloaded { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Total => Downloaded + Skipped + Failed;
}
