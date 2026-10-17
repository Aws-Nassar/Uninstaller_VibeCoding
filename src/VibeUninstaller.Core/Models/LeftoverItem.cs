namespace VibeUninstaller.Core.Models;

/// <summary>Confidence bucket for a leftover candidate.</summary>
public enum RiskTier
{
    /// <summary>Strong evidence: inside the app's own folders, product code match, ...</summary>
    Safe,

    /// <summary>Good evidence: names/publishers match but the location is generic.</summary>
    Moderate,

    /// <summary>Weak evidence: only shown in advanced mode.</summary>
    Advanced,
}

/// <summary>A leftover item proposed for removal after an uninstall.</summary>
public sealed record LeftoverItem
{
    public required string Path { get; init; }

    public required SystemItemKind Kind { get; init; }

    public required RiskTier Tier { get; init; }

    /// <summary>0..1 confidence score.</summary>
    public required double Score { get; init; }

    /// <summary>Human readable explanation of why this item was flagged.</summary>
    public required string Reason { get; init; }

    public bool CreatedDuringUninstall { get; init; }

    /// <summary>Size in bytes for files/folders. Null when unknown or not computed yet.</summary>
    public long? SizeBytes { get; set; }

    public bool IsRegistry => Kind == SystemItemKind.RegistryKey;

    public string TierLabel => Tier switch
    {
        RiskTier.Safe => "Safe",
        RiskTier.Moderate => "Medium",
        _ => "Advanced",
    };

    public string KindLabel => Kind switch
    {
        SystemItemKind.RegistryKey => "Registry",
        SystemItemKind.Directory => "Folder",
        _ => "File",
    };
}

/// <summary>Result of a leftover scan performed after an uninstall.</summary>
public sealed record LeftoverScanResult
{
    public required IReadOnlyList<LeftoverItem> Items { get; init; }

    public required int RegistryVanishedCount { get; init; }

    public required int FilesVanishedCount { get; init; }

    public TimeSpan Duration { get; init; }

    public int SafeCount => Items.Count(i => i.Tier == RiskTier.Safe);

    public int ModerateCount => Items.Count(i => i.Tier == RiskTier.Moderate);

    public int AdvancedCount => Items.Count(i => i.Tier == RiskTier.Advanced);

    public static LeftoverScanResult Empty { get; } = new()
    {
        Items = Array.Empty<LeftoverItem>(),
        RegistryVanishedCount = 0,
        FilesVanishedCount = 0,
    };
}
