using System.Text;
using System.Text.Json;
using SunoManager.Core.Models;

namespace SunoManager.Core.Services;

public class SunoApiClient(HttpClient http, SunoConfig config)
{
    private const string ApiBase = "https://studio-api-prod.suno.com";

    private void ApplyHeaders(HttpRequestMessage req)
    {
        req.Headers.Clear();
        req.Headers.TryAddWithoutValidation("Authorization", config.AuthToken.StartsWith("Bearer ")
            ? config.AuthToken : "Bearer " + config.AuthToken);
        req.Headers.TryAddWithoutValidation("browser-token", BuildBrowserToken());
        req.Headers.TryAddWithoutValidation("device-id", config.DeviceId);
        req.Headers.TryAddWithoutValidation("referer", "https://suno.com/");
        req.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/148.0.0.0 Safari/537.36");
    }

    private static string BuildBrowserToken()
    {
        var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var inner = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"timestamp\":{ms}}}"));
        return $"{{\"token\":\"{inner}\"}}";
    }

    public async Task<List<Models.Playlist>> GetAllPlaylistsAsync()
    {
        var all = new List<Models.Playlist>();
        var seen = new HashSet<string>();
        int page = 1;

        while (true)
        {
            var url = $"{ApiBase}/api/playlist/me?page={page}&show_trashed=false&show_sharelist=false";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(req);

            var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<PlaylistListResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response?.Playlists is null or { Count: 0 }) break;

            // The API repeats the first page and reports an unreliable total,
            // so dedupe by id and stop when a page contributes nothing new.
            int added = 0;
            foreach (var pl in response.Playlists)
            {
                if (string.IsNullOrEmpty(pl.Id) || !seen.Add(pl.Id)) continue;
                all.Add(pl);
                added++;
            }

            if (added == 0) break;
            page++;
        }

        return all;
    }

    // Returns playlist with all clips populated (handles pagination internally)
    public async Task<Models.Playlist?> GetPlaylistAsync(string playlistId)
    {
        Models.Playlist? result = null;
        var seen = new HashSet<string>();
        int page = 1;

        while (true)
        {
            var url = $"{ApiBase}/api/playlist/{playlistId}/?page={page}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(req);

            var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<Models.Playlist>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null) break;
            result ??= parsed;

            // The detail endpoint reports an unreliable total and repeats the
            // first page, so dedupe by clip id and stop when a page adds
            // nothing new (or comes back empty).
            int added = 0;
            foreach (var pc in parsed.Clips)
            {
                var id = pc.Clip?.Id;
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                if (!ReferenceEquals(parsed, result))
                    result.Clips.Add(pc);
                added++;
            }

            if (added == 0) break;
            page++;
        }

        return result;
    }

    public async Task<string?> GetDownloadUrlAsync(string clipId, string format = "mp3")
    {
        var url = $"{ApiBase}/api/download/clip/{clipId}?format={format}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(req);

        var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;

        var body = await res.Content.ReadAsStringAsync();
        var parsed = JsonSerializer.Deserialize<DownloadUrlResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Fall back to predictable CDN URL if API didn't return one
        return parsed?.Url ?? $"https://cdn1.suno.ai/{clipId}.{format}";
    }

    public async Task LogDownloadAsync(string clipId)
    {
        var url = $"{ApiBase}/api/billing/clips/{clipId}/download/";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyHeaders(req);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        try { await http.SendAsync(req); }
        catch { /* non-critical — don't fail the download over this */ }
    }

    public string RawDump_LastResponse { get; private set; } = "";

    public async Task<string> GetRawAsync(string relativeUrl)
    {
        var url = relativeUrl.StartsWith("http") ? relativeUrl : $"{ApiBase}{relativeUrl}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(req);
        var res = await http.SendAsync(req);
        RawDump_LastResponse = await res.Content.ReadAsStringAsync();
        return RawDump_LastResponse;
    }
}
