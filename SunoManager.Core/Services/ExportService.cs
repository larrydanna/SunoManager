namespace SunoManager.Core.Services;

public class ExportService(SunoConfig config)
{
    public ExportResult Export(bool dryRun = false, IProgress<string>? progress = null)
    {
        var result = new ExportResult();

        if (!Directory.Exists(config.LibraryPath))
        {
            result.Error = $"Library path not found: {config.LibraryPath}";
            return result;
        }

        if (string.IsNullOrWhiteSpace(config.UsbPath))
        {
            result.Error = "UsbPath is not configured.";
            return result;
        }

        if (!dryRun) Directory.CreateDirectory(config.UsbPath);

        foreach (var sourceFile in Directory.EnumerateFiles(config.LibraryPath, "*", SearchOption.AllDirectories))
        {
            // Skip the manifest file
            if (Path.GetFileName(sourceFile).StartsWith('.')) continue;

            var relative = Path.GetRelativePath(config.LibraryPath, sourceFile);
            var dest = Path.Combine(config.UsbPath, relative);

            if (!dryRun) Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            var needsCopy = !File.Exists(dest)
                || new FileInfo(sourceFile).Length != new FileInfo(dest).Length;

            if (needsCopy)
            {
                progress?.Report($"  {(dryRun ? "[dry-run] " : "")}Copy: {relative}");
                if (!dryRun) File.Copy(sourceFile, dest, overwrite: true);
                result.Copied++;
            }
            else
            {
                result.Unchanged++;
            }
        }

        return result;
    }
}

public class ExportResult
{
    public int Copied { get; set; }
    public int Unchanged { get; set; }
    public string? Error { get; set; }
}
