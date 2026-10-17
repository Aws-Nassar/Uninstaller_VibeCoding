using System.Text.Json;
using System.Text.Json.Serialization;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Execution;

public sealed record RemovalReport
{
    public required int FilesRemoved { get; init; }

    public required int FilesFailed { get; init; }

    public required int RegistryRemoved { get; init; }

    public required int RegistryFailed { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public string? VaultPath { get; init; }

    public long BytesRemoved { get; init; }
}

/// <summary>
/// Deletes the reviewed leftovers. Files go to the Recycle Bin and every
/// registry key is exported to a .reg backup first, so nothing is ever lost
/// silently.
/// </summary>
public sealed class RemovalExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RegistryManipulator _registry = new();
    private readonly FileRemover _fileRemover = new();

    public async Task<RemovalReport> RemoveAsync(
        InstalledApp app,
        IReadOnlyList<LeftoverItem> items,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var vaultPath = CreateVaultPath(app);
        var registryBackupPath = Path.Combine(vaultPath, "registry");

        var registryItems = items
            .Where(item => item.IsRegistry)
            .OrderByDescending(item => PathTools.SplitSegments(item.Path).Length)
            .ToArray();

        var errors = new List<string>();
        var registryRemoved = 0;
        var registryFailed = 0;

        foreach (var item in registryItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backupFile = Path.Combine(registryBackupPath, $"key-{registryRemoved + registryFailed:D4}.reg");
            if (!_registry.ExportKey(item.Path, backupFile, out var exportError))
            {
                registryFailed++;
                errors.Add($"Backup failed, key kept: {item.Path} ({exportError})");
                continue;
            }

            progress?.Report($"Removing registry key {item.Path}");
            if (_registry.DeleteKey(item.Path, out var deleteError))
            {
                registryRemoved++;
            }
            else
            {
                registryFailed++;
                errors.Add($"Could not delete {item.Path}: {deleteError}");
            }
        }

        var filePaths = items
            .Where(item => !item.IsRegistry)
            .Select(item => item.Path)
            .ToArray();

        var fileResult = filePaths.Length > 0
            ? _fileRemover.MoveToRecycleBin(filePaths, progress)
            : new FileRemovalResult(0, 0, Array.Empty<string>());

        errors.AddRange(fileResult.Errors);

        var bytesRemoved = items.Sum(item => item.SizeBytes ?? 0);

        var report = new RemovalReport
        {
            FilesRemoved = fileResult.Attempted - fileResult.Failed,
            FilesFailed = fileResult.Failed,
            RegistryRemoved = registryRemoved,
            RegistryFailed = registryFailed,
            Errors = errors,
            VaultPath = vaultPath,
            BytesRemoved = bytesRemoved,
        };

        await WriteManifestAsync(vaultPath, app, items, report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private static string CreateVaultPath(InstalledApp app)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "VibeUninstaller",
            "Vault");

        var safeName = string.Join('_', app.DisplayName
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        if (safeName.Length == 0)
        {
            safeName = "app";
        }

        if (safeName.Length > 60)
        {
            safeName = safeName[..60];
        }

        var path = Path.Combine(root, $"{DateTime.Now:yyyyMMdd-HHmmss}-{safeName}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteManifestAsync(
        string vaultPath,
        InstalledApp app,
        IReadOnlyList<LeftoverItem> items,
        RemovalReport report,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            application = new
            {
                app.DisplayName,
                app.Publisher,
                app.Version,
                app.InstallLocation,
                app.RegistryKeyPath,
                app.ProductCode,
                Source = app.Source.ToString(),
            },
            removedAt = DateTimeOffset.Now,
            summary = new
            {
                report.FilesRemoved,
                report.FilesFailed,
                report.RegistryRemoved,
                report.RegistryFailed,
                report.BytesRemoved,
            },
            items = items.Select(item => new
            {
                item.Path,
                Kind = item.Kind.ToString(),
                Tier = item.Tier.ToString(),
                item.Score,
                item.Reason,
                item.CreatedDuringUninstall,
                item.SizeBytes,
            }),
            errors = report.Errors,
        };

        await using var stream = File.Create(Path.Combine(vaultPath, "manifest.json"));
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
