# SunoManager.Core

Shared library used by both CLI and MCP hosts.

## Responsibilities

- API configuration and token handling (`SunoConfig`, `TokenStore`)
- Suno API client (`Services/SunoApiClient.cs`)
- Playlist/song models (`Models/`)
- Local sync/export/manifests (`Services/DownloadService.cs`, `Services/ManifestService.cs`, `Services/ExportService.cs`)

## Notable Behavior

- `TokenStore` uses `%APPDATA%/SunoManager/token.json` as the shared cache file.
- Secure token persistence is available on Windows (`ProtectedData` / DPAPI).
- `SunoConfig.IsTokenExpired()` treats near-expiry tokens (<= 2 minutes remaining) as expired.

## Referenced By

- `SunoManager.Cli`
- `SunoManager.Mcp`
