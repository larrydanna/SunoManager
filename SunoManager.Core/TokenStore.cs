using System.Text.Json;
using System.Text;
using System.Security.Cryptography;

namespace SunoManager.Core;

public static class TokenStore
{
    private const string TokenEntropy = "SunoManager.TokenStore.v1";

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SunoManager", "token.json");

    public static void Save(string bearerToken)
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var normalized = bearerToken.StartsWith("Bearer ") ? bearerToken : "Bearer " + bearerToken;
        var encryptedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(normalized),
            Encoding.UTF8.GetBytes(TokenEntropy),
            DataProtectionScope.CurrentUser);

        var json = JsonSerializer.Serialize(new
        {
            Suno = new
            {
                AuthTokenProtected = Convert.ToBase64String(encryptedBytes),
                Protection = "CurrentUserDpapi"
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(FilePath, json);
    }

    // Reads the saved bearer token from token.json, or null if absent/unreadable.
    // This is the shared hand-off point: the CLI ('suno token') writes the file,
    // any MCP host reads it back -- the token never travels through a model.
    public static string? TryRead()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.TryGetProperty("Suno", out var suno)
                && suno.TryGetProperty("AuthTokenProtected", out var protectedToken))
            {
                var protectedValue = protectedToken.GetString();
                if (string.IsNullOrWhiteSpace(protectedValue)) return null;
                if (!OperatingSystem.IsWindows()) return null;

                var decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(protectedValue),
                    Encoding.UTF8.GetBytes(TokenEntropy),
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }

            // Backward compatibility with older plain-text cache.
            if (doc.RootElement.TryGetProperty("Suno", out suno)
                && suno.TryGetProperty("AuthToken", out var legacyToken))
                return legacyToken.GetString();
        }
        catch { /* unreadable or malformed — treat as absent */ }
        return null;
    }

    public static bool IsSecureCacheAvailable() => OperatingSystem.IsWindows();

    public static (bool valid, DateTimeOffset expiry, string error) Validate(string rawToken)
    {
        var token = rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? rawToken[7..] : rawToken;

        var parts = token.Split('.');
        if (parts.Length < 2)
            return (false, default, "Not a valid JWT (expected 3 parts separated by dots).");

        try
        {
            var padding = (4 - parts[1].Length % 4) % 4;
            var base64 = parts[1].Replace('-', '+').Replace('_', '/') + new string('=', padding);
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("exp", out var exp))
                return (false, default, "JWT has no 'exp' claim.");

            var expiry = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (expiry <= DateTimeOffset.UtcNow)
                return (false, expiry, $"Token already expired at {expiry.LocalDateTime:HH:mm}.");

            return (true, expiry, "");
        }
        catch (Exception ex)
        {
            return (false, default, $"Could not decode JWT payload: {ex.Message}");
        }
    }
}
