# SunoManager

## Project Documentation

- [SunoManager.Cli](SunoManager.Cli/README.md) - command-line sync and export workflow
- [SunoManager.Core](SunoManager.Core/README.md) - shared models and services
- [SunoManager.Mcp](SunoManager.Mcp/README.md) - MCP server setup and tool interface


## Questions

|Q & A||
|-|-|
|:red_circle:|Is gstack being used when working on this?|
|Answer:||

## Links
[Network Captures for API Discovery](artifacts\network-capture.txt)

[Suno API Documentation](https://docs.sunoapi.org/)

## Credential Cache

Set `Suno:AllowCredentialCache` to `true` in `appsettings.local.json` if you want SunoManager to cache your token locally in a protected store (`%APPDATA%/SunoManager/token.json` on Windows). If disabled (default), SunoManager continues normal flow without cached login.

## Todo

|Status|As Of|Task|
|-|-|-|
🟩|5/22/2026, 5:13:08 AM|git initialize
🟩|5/22/2026, 5:13:08 AM|Push to GitHub larrydanna
🟩|5/22/2026, 2:27:08 AM|Document the MCP interface and all the projects, README.md at each interesting level
🟩|5/22/2026, 5:13:08 AM|Add secure credential cache, let me store my login locally
🟩|5/22/2026, 5:13:08 AM|Pull your own bearer token when needed, store it
🟩|5/22/2026, 5:13:08 AM|Add master playlist in the root
🟩|5/22/2026, 5:13:08 AM|Add Title to lyrics txt
🟩|5/22/2026, 5:13:08 AM|CLI Help
:red_circle:|5/22/2026, 5:12:37 AM|Modify workstation config to NOT start in EVERY client. Save your context.
