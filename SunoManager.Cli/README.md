# SunoManager.Cli

Console entry point for local Suno library management.

## Commands

- `suno token` - refresh and store auth token in shared `token.json`
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
