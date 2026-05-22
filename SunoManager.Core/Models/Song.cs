using System.Text.Json.Serialization;

namespace SunoManager.Core.Models;

public class Song
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("audio_url")]
    public string? AudioUrl { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_large_url")]
    public string? ImageLargeUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    // Suno nests lyrics, style tags and duration inside a "metadata" object.
    [JsonPropertyName("metadata")]
    public SongMetadata? Metadata { get; set; }

    // The "prompt" field holds the full lyrics (with [Verse]/[Chorus] markers).
    public string? Lyrics => Metadata?.Prompt;

    // Freeform style description Suno used to generate the track.
    public string? StyleTags => Metadata?.Tags;

    public double? DurationSeconds => Metadata?.Duration;

    // Derived: always constructable from Id regardless of API shape.
    public string CdnMp3Url => $"https://cdn1.suno.ai/{Id}.mp3";
    public string CdnImageUrl => $"https://cdn2.suno.ai/image_{Id}.jpeg";

    // Best available artwork URL — large variant preferred for embedding.
    public string BestArtUrl => ImageLargeUrl ?? ImageUrl ?? CdnImageUrl;

    public string SafeTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled"
        : string.Concat(Title.Split(Path.GetInvalidFileNameChars()));
}

public class SongMetadata
{
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}

public class PlaylistClip
{
    [JsonPropertyName("clip")]
    public Song? Clip { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}
