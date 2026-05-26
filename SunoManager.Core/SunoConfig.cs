namespace SunoManager.Core;

public class SunoConfig
{
    public string AuthToken { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Artist { get; set; } = "";
    public string LibraryPath { get; set; } = "";
    public string UsbPath { get; set; } = "";
    public string Format { get; set; } = "mp3";
    public bool IncludeCoverArt { get; set; } = true;

    public bool IsTokenExpired()
    {
        if (string.IsNullOrWhiteSpace(AuthToken)) return true;
        var token = AuthToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? AuthToken[7..] : AuthToken;

        var parts = token.Split('.');
        if (parts.Length < 2) return true;

        var padding = (4 - parts[1].Length % 4) % 4;
        var base64 = parts[1].Replace('-', '+').Replace('_', '/') + new string('=', padding);

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
                return expiry <= DateTimeOffset.UtcNow.AddMinutes(2);
            }
        }
        catch { }
        return true;
    }
}
