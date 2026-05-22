# SunoManager.Cli

Console entry point for local Suno library management.

## Run

From repository root:

```bash
dotnet run --project SunoManager.Cli -- <command> [options]
```

## Commands

- `suno token` - refresh and store auth token in shared `token.json`
- `suno refresh-token` - alias of `token`
- `suno sync` - sync all playlists
- `suno sync --playlist "Name"` - sync a single playlist
- `suno export` - copy library to configured USB path
- `suno export --dry-run` - preview export actions
- `suno dump [path]` - dump raw API response for debugging

## Config

Reads from:

1. `appsettings.json`
2. `appsettings.local.json` (optional)
3. shared token file (`%APPDATA%/SunoManager/token.json` on Windows)

`appsettings.local.json` is gitignored (`**/appsettings.local.json`) and is the recommended place for local overrides.
