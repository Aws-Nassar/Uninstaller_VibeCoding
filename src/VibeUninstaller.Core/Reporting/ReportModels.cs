using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Core.Reporting;

/// <summary>
/// Stable, documented JSON shapes used by the CLI (and later by automation).
/// These types are the public contract: renaming a property is a breaking change.
/// </summary>
public sealed record AppReport
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Version { get; init; }

    public string? Publisher { get; init; }

    public required string Source { get; init; }

    public required string Scope { get; init; }

    public string? InstallLocation { get; init; }

    public long? EstimatedSizeBytes { get; init; }

    public string? InstallDate { get; init; }

    public bool IsSystemComponent { get; init; }

    public bool IsUpdate { get; init; }

    public string? UninstallString { get; init; }

    public string? QuietUninstallString { get; init; }

    public string? ProductCode { get; init; }

    public string? RegistryKeyPath { get; init; }

    public string? PackageFullName { get; init; }

    public string? SteamAppId { get; init; }

    public static AppReport From(InstalledApp app) => new()
    {
        Id = app.Id,
        Name = app.DisplayName,
        Version = app.Version,
        Publisher = app.Publisher,
        Source = app.Source.ToString().ToLowerInvariant(),
        Scope = app.Scope == InstallScope.Machine ? "machine" : "user",
        InstallLocation = app.InstallLocation,
        EstimatedSizeBytes = app.EstimatedSizeBytes,
        InstallDate = app.InstallDate?.ToString("yyyy-MM-dd"),
        IsSystemComponent = app.IsSystemComponent,
        IsUpdate = app.IsUpdate,
        UninstallString = app.UninstallString,
        QuietUninstallString = app.QuietUninstallString,
        ProductCode = app.ProductCode,
        RegistryKeyPath = app.RegistryKeyPath,
        PackageFullName = app.PackageFullName,
        SteamAppId = app.SteamAppId,
    };
}

public sealed record LeftoverReport
{
    public required string Path { get; init; }

    /// <summary>One of registry, directory, file.</summary>
    public required string Kind { get; init; }

    /// <summary>One of safe, moderate, advanced.</summary>
    public required string Tier { get; init; }

    public required double Score { get; init; }

    public required string Reason { get; init; }

    public bool CreatedDuringUninstall { get; init; }

    public long? SizeBytes { get; init; }

    public static LeftoverReport From(LeftoverItem item) => new()
    {
        Path = item.Path,
        Kind = item.Kind switch
        {
            SystemItemKind.RegistryKey => "registry",
            SystemItemKind.Directory => "directory",
            _ => "file",
        },
        Tier = item.Tier switch
        {
            RiskTier.Safe => "safe",
            RiskTier.Moderate => "moderate",
            _ => "advanced",
        },
        Score = Math.Round(item.Score, 2),
        Reason = item.Reason,
        CreatedDuringUninstall = item.CreatedDuringUninstall,
        SizeBytes = item.SizeBytes,
    };
}

public sealed record ScanReport
{
    /// <summary>static for a fingerprint scan, post-uninstall for a diff based scan.</summary>
    public required string Mode { get; init; }

    public required int Total { get; init; }

    public required int Safe { get; init; }

    public required int Moderate { get; init; }

    public required int Advanced { get; init; }

    public long? SizeBytes { get; init; }

    public required long DurationMs { get; init; }

    public required IReadOnlyList<LeftoverReport> Items { get; init; }

    public static ScanReport From(string mode, LeftoverScanResult scan, long? sizeBytes = null) => new()
    {
        Mode = mode,
        Total = scan.Items.Count,
        Safe = scan.SafeCount,
        Moderate = scan.ModerateCount,
        Advanced = scan.AdvancedCount,
        SizeBytes = sizeBytes,
        DurationMs = (long)scan.Duration.TotalMilliseconds,
        Items = scan.Items.Select(LeftoverReport.From).ToArray(),
    };
}

public sealed record RunReport
{
    public required string Status { get; init; }

    public int? ExitCode { get; init; }

    public bool RebootRequired { get; init; }

    public required long DurationMs { get; init; }

    public int ProcessCount { get; init; }

    public string? CommandLine { get; init; }

    public string? Error { get; init; }

    public static RunReport From(UninstallRunResult run) => new()
    {
        Status = run.Status.ToString().ToLowerInvariant(),
        ExitCode = run.ExitCode,
        RebootRequired = run.RebootRequired,
        DurationMs = (long)run.Duration.TotalMilliseconds,
        ProcessCount = run.ProcessCount,
        CommandLine = run.CommandLine,
        Error = run.ErrorMessage,
    };
}

public sealed record RemovalReportJson
{
    public required int FilesRemoved { get; init; }

    public required int FilesFailed { get; init; }

    public required int RegistryRemoved { get; init; }

    public required int RegistryFailed { get; init; }

    public required long BytesRemoved { get; init; }

    public string? VaultPath { get; init; }

    /// <summary>True when registry keys were deleted without a .reg backup.</summary>
    public bool RegistryBackupSkipped { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }
}

public sealed record ListReport
{
    public required int Count { get; init; }

    public required IReadOnlyList<AppReport> Apps { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record InfoReport
{
    public required AppReport App { get; init; }

    public required string InstallerFamily { get; init; }

    public bool SilentSupported { get; init; }

    public string? SilentCommand { get; init; }

    public required IReadOnlyList<string> NameTokens { get; init; }

    public required IReadOnlyList<string> VendorTokens { get; init; }

    public required IReadOnlyList<string> InstallRoots { get; init; }

    public required IReadOnlyList<string> RootCandidates { get; init; }

    public required string LeftoverScanSupport { get; init; }
}

public sealed record OperationReport
{
    public required AppReport App { get; init; }

    public RunReport? Run { get; init; }

    public ScanReport? Scan { get; init; }

    public RemovalReportJson? Removal { get; init; }

    /// <summary>True when the run only showed what would happen.</summary>
    public bool DryRun { get; init; }

    public required string Outcome { get; init; }

    public required IReadOnlyList<string> Messages { get; init; }
}

/// <summary>Serialization settings shared by every JSON producing command.</summary>
public static class ReportJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
