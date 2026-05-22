using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using SunoManager.Core;
using SunoManager.Core.Services;
using SunoManager.Mcp;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true);

var sunoConfig = builder.Configuration.GetSection("Suno").Get<SunoConfig>()
    ?? new SunoConfig();
if (sunoConfig.AllowCredentialCache)
{
    var cachedToken = TokenStore.TryRead();
    if (!string.IsNullOrWhiteSpace(cachedToken))
        sunoConfig.AuthToken = cachedToken;
}

builder.Services.AddSingleton(sunoConfig);
builder.Services.AddHttpClient<SunoApiClient>();
builder.Services.AddHttpClient<DownloadService>();
builder.Services.AddSingleton<ManifestService>();
builder.Services.AddSingleton<ExportService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SunoTools>();

await builder.Build().RunAsync();
