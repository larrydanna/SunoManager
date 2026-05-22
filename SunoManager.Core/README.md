# SunoManager.Core

Shared library for SunoManager.

## Responsibilities

- API configuration and token handling (`SunoConfig`, `TokenStore`)
- Suno API client (`Services/SunoApiClient.cs`)
- Playlist/song models (`Models/`)
- Local sync/export/manifests (`Services/DownloadService.cs`, `Services/ManifestService.cs`, `Services/ExportService.cs`)

## Referenced By

- `SunoManager.Cli`
- `SunoManager.Mcp`
