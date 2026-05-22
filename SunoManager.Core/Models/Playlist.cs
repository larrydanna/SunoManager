using System.Text.Json.Serialization;

namespace SunoManager.Core.Models;

public class Playlist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("song_count")]
    public int SongCount { get; set; }

    [JsonPropertyName("num_total_results")]
    public int TotalClips { get; set; }

    [JsonPropertyName("playlist_clips")]
    public List<PlaylistClip> Clips { get; set; } = [];

    public int DisplayCount => SongCount > 0 ? SongCount : TotalClips;

    public string SafeName => string.IsNullOrWhiteSpace(Name) ? Id
        : string.Concat(Name.Split(Path.GetInvalidPathChars()));
}

public class PlaylistListResponse
{
    [JsonPropertyName("playlists")]
    public List<Playlist> Playlists { get; set; } = [];

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("num_total_results")]
    public int NumTotalResults { get; set; }

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("num_pages")]
    public int NumPages { get; set; }
}

public class DownloadUrlResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}
