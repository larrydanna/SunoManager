# SunoManager

SunoManager syncs your Suno playlists to a local library and can export that library to removable storage.

## Projects

- [SunoManager.Cli](SunoManager.Cli/README.md): command-line workflow for token refresh, sync, export, and API debugging
- [SunoManager.Core](SunoManager.Core/README.md): shared models, configuration, token storage, and sync/export services
- [SunoManager.Mcp](SunoManager.Mcp/README.md): MCP server exposing SunoManager operations as tools

## Prerequisites

- .NET SDK 9.0+
- A valid Suno bearer token

## Quick Start

1. Review and update:
   - `SunoManager.Cli/appsettings.json`
   - `SunoManager.Mcp/appsettings.json`
2. (Optional, recommended) Create `appsettings.local.json` in either project for local overrides.
3. Refresh token via CLI:
   ```bash
   dotnet run --project SunoManager.Cli -- token
   ```
4. Sync playlists:
   ```bash
   dotnet run --project SunoManager.Cli -- sync
   ```
5. Export local library:
   ```bash
   dotnet run --project SunoManager.Cli -- export
   ```

## Build and Validation

```bash
dotnet build SunoManager.sln
dotnet test SunoManager.sln
```

## Credential Cache

Set `Suno:AllowCredentialCache` to `true` to allow token caching in `%APPDATA%/SunoManager/token.json` (secure DPAPI protection on Windows). When disabled (default), tokens are used only for the current run.

## References

- [Network Captures for API Discovery](artifacts/network-capture.txt)
- [Suno API Documentation](https://docs.sunoapi.org/)
