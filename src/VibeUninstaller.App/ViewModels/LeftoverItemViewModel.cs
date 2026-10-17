using CommunityToolkit.Mvvm.ComponentModel;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.App.ViewModels;

/// <summary>One candidate row in the leftover review list.</summary>
public sealed partial class LeftoverItemViewModel : ObservableObject
{
    public LeftoverItemViewModel(LeftoverItem item)
    {
        Item = item;
        _sizeBytes = item.SizeBytes;
        _isSelected = item.Tier != RiskTier.Advanced;
    }

    public LeftoverItem Item { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private long? _sizeBytes;

    public string Path => Item.Path;

    public string Reason => Item.Reason;

    public RiskTier Tier => Item.Tier;

    public string TierLabel => Item.TierLabel;

    public string KindLabel => Item.KindLabel;

    public bool CreatedDuringUninstall => Item.CreatedDuringUninstall;
}
