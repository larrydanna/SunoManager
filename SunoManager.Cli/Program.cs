using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunoManager.Core;
using SunoManager.Core.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true)
    .Build();

var sunoConfig = config.GetSection("Suno").Get<SunoConfig>()
    ?? throw new InvalidOperationException("Missing [Suno] section in appsettings.json");
if (sunoConfig.AllowCredentialCache)
{
    var cachedToken = TokenStore.TryRead();
    if (!string.IsNullOrWhiteSpace(cachedToken))
        sunoConfig.AuthToken = cachedToken;
}

var services = new ServiceCollection();
services.AddSingleton(sunoConfig);
services.AddHttpClient<SunoApiClient>();
services.AddHttpClient<DownloadService>();
services.AddSingleton<ManifestService>();
services.AddSingleton<ExportService>();
var sp = services.BuildServiceProvider();

var command = args.Length > 0 ? args[0].ToLower() : "help";

switch (command)
{
    case "sync":
        await RunSync(args.Skip(1).ToArray(), sp, sunoConfig);
        break;

    case "export":
        RunExport(args.Skip(1).ToArray(), sp, sunoConfig);
        break;

    case "refresh-token":
    case "token":
        RefreshToken(sunoConfig);
        break;

    case "dump":
        await RunDump(args.Skip(1).ToArray(), sp, sunoConfig);
        break;

    default:
        ShowHelp();
        break;
}

static async Task RunSync(string[] args, IServiceProvider sp, SunoConfig config)
{
    if (config.IsTokenExpired())
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Token expired. Run:  suno token");
        Console.ResetColor();
        return;
    }

    var apiClient = sp.GetRequiredService<SunoApiClient>();
    var downloader = sp.GetRequiredService<DownloadService>();
    var progress = new Progress<string>(Console.WriteLine);

    string? targetPlaylist = null;
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] is "--playlist" or "-p") { targetPlaylist = args[i + 1]; break; }

    Console.WriteLine("Fetching playlists...");
    var playlists = await apiClient.GetAllPlaylistsAsync();

    if (targetPlaylist is not null)
        playlists = playlists.Where(p =>
            p.Name.Equals(targetPlaylist, StringComparison.OrdinalIgnoreCase)).ToList();

    if (playlists.Count == 0)
    {
        Console.WriteLine(targetPlaylist is null
            ? "No playlists found." : $"Playlist not found: {targetPlaylist}");
        return;
    }

    foreach (var summary in playlists)
    {
        Console.WriteLine($"\nPlaylist: {summary.Name} ({summary.DisplayCount} songs) — fetching clips...");
        var playlist = await apiClient.GetPlaylistAsync(summary.Id);
        if (playlist is null) { Console.WriteLine("  Could not fetch playlist detail, skipping."); continue; }

        var result = await downloader.SyncPlaylistAsync(playlist, progress);
        Console.WriteLine($"  Done -- {result.Downloaded} downloaded, {result.Skipped} skipped, {result.Failed} failed");
    }

    await downloader.WriteMasterPlaylistAsync(progress);
}

static void RunExport(string[] args, IServiceProvider sp, SunoConfig config)
{
    if (string.IsNullOrWhiteSpace(config.UsbPath))
    {
        Console.WriteLine("UsbPath is not set in appsettings.json.");
        return;
    }

    var dryRun = args.Contains("--dry-run");
    var exporter = sp.GetRequiredService<ExportService>();
    var progress = new Progress<string>(Console.WriteLine);

    Console.WriteLine(dryRun
        ? $"[dry-run] Would export to: {config.UsbPath}"
        : $"Exporting to: {config.UsbPath}");

    var result = exporter.Export(dryRun, progress);

    if (result.Error is not null)
        Console.WriteLine($"ERROR: {result.Error}");
    else
        Console.WriteLine($"Done -- {result.Copied} copied, {result.Unchanged} unchanged");
}

static void RefreshToken(SunoConfig config)
{
    Console.WriteLine();
    Console.WriteLine("=== Suno Token Refresh ===");
    Console.WriteLine();
    Console.WriteLine("Steps to get a fresh token:");
    Console.WriteLine("  1. Open Chrome and go to https://suno.com");
    Console.WriteLine("  2. Open DevTools  (F12)");
    Console.WriteLine("  3. Click the Network tab");
    Console.WriteLine("  4. Click on any request to studio-api-prod.suno.com");
    Console.WriteLine("     (browse around the site if none appear)");
    Console.WriteLine("  5. In the Headers panel, find: Authorization");
    Console.WriteLine("  6. Copy the entire value  (starts with 'Bearer eyJ...')");
    Console.WriteLine();
    Console.Write("Paste token here: ");

    var input = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(input))
    {
        Console.WriteLine("Nothing pasted. Cancelled.");
        return;
    }

    var (valid, expiry, error) = TokenStore.Validate(input);
    if (!valid)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Invalid token: {error}");
        Console.ResetColor();
        return;
    }

    if (config.AllowCredentialCache && TokenStore.IsSecureCacheAvailable())
    {
        TokenStore.Save(input);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Token saved. Valid until {expiry.LocalDateTime:ddd MMM d, h:mm tt} (about {(int)(expiry - DateTimeOffset.UtcNow).TotalMinutes} minutes from now).");
        Console.ResetColor();
        Console.WriteLine($"Stored at: {TokenStore.FilePath}");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Token validated. Valid until {expiry.LocalDateTime:ddd MMM d, h:mm tt}.");
        Console.WriteLine("Secure credential cache is disabled or unavailable, so the token was not stored.");
        Console.ResetColor();
    }
    Console.WriteLine();
    Console.WriteLine("You can now run:  suno sync");
}

static async Task RunDump(string[] args, IServiceProvider sp, SunoConfig config)
{
    if (config.IsTokenExpired())
    {
        Console.WriteLine("Token expired. Run:  suno token");
        return;
    }

    var apiClient = sp.GetRequiredService<SunoApiClient>();
    var path = args.Length > 0 ? args[0] : "/api/playlist/me?page=1&show_trashed=false&show_sharelist=false";
    Console.WriteLine($"GET {path}\n");
    var raw = await apiClient.GetRawAsync(path);

    try
    {
        var doc = System.Text.Json.JsonDocument.Parse(raw);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(doc,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    catch { Console.WriteLine(raw); }
}

static void ShowHelp()
{
    Console.WriteLine("""
        SunoManager -- sync your Suno playlists to local folders

        COMMANDS
          token                       Walk through refreshing your Suno auth token
          sync                        Download all playlists
          sync --playlist "Name"      Download a single playlist
          export                      Copy library to USB drive
          export --dry-run            Preview what would be copied
          dump                        Dump /api/playlist/me response (for debugging)
          dump /api/some/path         Dump any API path raw JSON

        TOKEN
          Tokens expire every ~60 minutes. Run 'suno token' whenever sync says it's expired.
          Set Suno:AllowCredentialCache=true to store a protected token at: %APPDATA%\SunoManager\token.json

        CONFIG
          appsettings.json            Main config (LibraryPath, UsbPath, etc.)
          appsettings.local.json      Local overrides (gitignored)
        """);
}
