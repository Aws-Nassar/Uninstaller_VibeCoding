using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.App.ViewModels;

/// <summary>One row in the application list.</summary>
public sealed class AppItemViewModel
{
    private static readonly string[] Palette =
    {
        "#4C8DFF", "#9B6DFF", "#2FA96B", "#D29922", "#E5534B", "#15AABF", "#E8590C", "#7048E8",
    };

    public AppItemViewModel(InstalledApp app)
    {
        App = app;
        var trimmed = app.DisplayName.Trim();
        Lettermark = trimmed.Length > 0 ? char.ToUpperInvariant(trimmed[0]).ToString() : "?";
        var hash = trimmed.Aggregate(17, (current, character) => current * 31 + char.ToLowerInvariant(character));
        LettermarkBrush = CreateBrush(Palette[Math.Abs(hash) % Palette.Length]);

        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);
        Installer = InstallerInfoDetector.Detect(app, parsed);
    }

    public InstalledApp App { get; }

    public InstallerInfo Installer { get; }

    public string DisplayName => App.DisplayName;

    public string Publisher => string.IsNullOrWhiteSpace(App.Publisher) ? "—" : App.Publisher;

    public string Version => string.IsNullOrWhiteSpace(App.Version) ? "—" : App.Version;

    public long? EstimatedSizeBytes => App.EstimatedSizeBytes;

    public string InstallDateText => App.InstallDate?.ToString("yyyy-MM-dd") ?? "—";

    public string SourceLabel => App.SourceLabel;

    public string ScopeLabel => App.ScopeLabel;

    public string InstallLocationText => string.IsNullOrWhiteSpace(App.InstallLocation) ? "Not reported" : App.InstallLocation;

    public string UninstallCommandText => string.IsNullOrWhiteSpace(App.UninstallString)
        ? "Not reported"
        : App.UninstallString;

    public string RegistryKeyText => App.RegistryKeyPath ?? "—";

    public string InstallerLabel => Installer.FamilyLabel;

    public string Lettermark { get; }

    public Brush LettermarkBrush { get; }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
