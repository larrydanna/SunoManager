# SunoManager.Mcp

Model Context Protocol (MCP) server for controlling SunoManager from MCP clients over stdio.

## Host Setup

- Entry point: `Program.cs`
- Transport: stdio (`WithStdioServerTransport`)
- Tool registration: `WithTools<SunoTools>()`
- Shared services come from `SunoManager.Core`

## Configuration

Loaded in this order:

1. `appsettings.json`
2. `appsettings.local.json` (optional)
3. shared token file (`%APPDATA%/SunoManager/token.json` on Windows)

The `set_token` tool can reload from the shared token file, so CLI and MCP can share auth.

## MCP Interface

`SunoTools` exposes these tools:

- `set_token(token?)` - activate/reload bearer token
- `list_playlists()` - list playlists with id and song counts
- `get_library_status()` - summarize local manifest and playlist counts
- `list_songs(playlist?)` - list locally downloaded songs
- `sync_playlist(playlistName)` - sync one named playlist
- `sync_all()` - sync all playlists
- `export_to_usb(dryRun=false)` - export local library to USB path
