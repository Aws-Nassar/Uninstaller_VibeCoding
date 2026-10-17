using System.Text.RegularExpressions;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Core.Execution;

public enum InstallerFamily
{
    Unknown,
    Msi,
    InnoSetup,
    Nsis,
    InstallShield,
    Squirrel,
    Steam,
    Appx,
}

public sealed record InstallerInfo
{
    public required InstallerFamily Family { get; init; }

    /// <summary>Arguments that make the uninstaller run unattended, when known.</summary>
    public string? SilentArguments { get; init; }

    /// <summary>Extra switches to append to a base command (for example "/qn /norestart").</summary>
    public string? SilentSuffixArguments { get; init; }

    public string? Note { get; init; }

    public bool SupportsSilent => SilentArguments is not null || SilentSuffixArguments is not null;

    public string FamilyLabel => Family switch
    {
        InstallerFamily.Msi => "Windows Installer (MSI)",
        InstallerFamily.InnoSetup => "Inno Setup",
        InstallerFamily.Nsis => "NSIS",
        InstallerFamily.InstallShield => "InstallShield",
        InstallerFamily.Squirrel => "Squirrel",
        InstallerFamily.Steam => "Steam",
        InstallerFamily.Appx => "Store package",
        _ => "Unknown installer",
    };
}

/// <summary>Recognizes the installer technology behind an app and its silent switches.</summary>
public static partial class InstallerInfoDetector
{
    private static readonly HashSet<string> NsisNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall.exe",
        "uninstaller.exe",
        "uninst.exe",
        "un_a.exe",
        "au_.exe",
        "uninstall_",
    };

    public static InstallerInfo Detect(InstalledApp app, (string FileName, string Arguments)? parsed)
    {
        if (app.Source == AppSource.Appx)
        {
            return new InstallerInfo { Family = InstallerFamily.Appx, Note = "Removed through the package manager" };
        }

        if (app.Source == AppSource.Steam)
        {
            return new InstallerInfo { Family = InstallerFamily.Steam, Note = "Handled by the Steam client" };
        }

        var fileName = parsed?.FileName ?? string.Empty;
        var arguments = parsed?.Arguments ?? string.Empty;
        var shortName = fileName.Length > 0 ? Path.GetFileName(fileName) : string.Empty;
        var productCode = app.ProductCode ?? CommandLineSplitter.TryGetMsiProductCode(app.UninstallString);

        var looksLikeMsi = productCode is not null
            || shortName.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase)
            || shortName.Equals("msiexec", StringComparison.OrdinalIgnoreCase);

        if (looksLikeMsi)
        {
            var code = productCode;
            return new InstallerInfo
            {
                Family = InstallerFamily.Msi,
                SilentArguments = code is not null ? $"/x {code} /qn /norestart" : null,
                Note = code is null ? "Product code unknown, silent switch unavailable" : null,
            };
        }

        if (InnoUninstallerRegex().IsMatch(shortName))
        {
            return new InstallerInfo
            {
                Family = InstallerFamily.InnoSetup,
                SilentSuffixArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            };
        }

        if (shortName.Equals("update.exe", StringComparison.OrdinalIgnoreCase)
            && arguments.Contains("--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallerInfo
            {
                Family = InstallerFamily.Squirrel,
                SilentArguments = "--uninstall -s",
            };
        }

        if (NsisNames.Contains(shortName) || shortName.StartsWith("uninstall_", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallerInfo
            {
                Family = InstallerFamily.Nsis,
                SilentSuffixArguments = "/S",
                Note = "Assumed NSIS based on the uninstaller name",
            };
        }

        if (arguments.Contains("-runfromtemp", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("InstallShield Installation Information", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallerInfo
            {
                Family = InstallerFamily.InstallShield,
                Note = "Silent switches for InstallShield are not supported yet",
            };
        }

        return new InstallerInfo { Family = InstallerFamily.Unknown };
    }

    /// <summary>
    /// Inno Setup names its uninstaller unins000.exe, unins001.exe and so on.
    /// A plain "uninstall.exe" is an NSIS installer, which is why the two
    /// patterns must not be confused.
    /// </summary>
    [GeneratedRegex(@"^unins\d+\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex InnoUninstallerRegex();

    /// <summary>
    /// Builds the command that should actually be executed, optionally preferring
    /// the app provided quiet uninstall string or the silent switch engine.
    /// </summary>
    public static InstallCommand BuildCommand(InstalledApp app, InstallerInfo info, bool preferSilent)
    {
        if (app.Source == AppSource.Steam)
        {
            return new InstallCommand
            {
                FileName = string.IsNullOrWhiteSpace(app.UninstallString)
                    ? $"steam://uninstall/{app.SteamAppId}"
                    : app.UninstallString,
                RequiresElevation = false,
            };
        }

        if (preferSilent && !string.IsNullOrWhiteSpace(app.QuietUninstallString))
        {
            var quiet = CommandLineSplitter.Split(app.QuietUninstallString);
            if (quiet is not null)
            {
                return new InstallCommand
                {
                    FileName = quiet.Value.FileName,
                    Arguments = quiet.Value.Arguments,
                    RequiresElevation = app.Scope == InstallScope.Machine,
                    IsSilent = true,
                };
            }
        }

        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);

        if (parsed is null)
        {
            return new InstallCommand
            {
                FileName = string.Empty,
                Arguments = string.Empty,
                RequiresElevation = app.Scope == InstallScope.Machine,
            };
        }

        var fileName = parsed.Value.FileName;
        var arguments = parsed.Value.Arguments;
        var isSilent = false;

        if (preferSilent && info.SupportsSilent)
        {
            if (info.SilentArguments is not null)
            {
                arguments = info.SilentArguments;
            }
            else if (info.SilentSuffixArguments is not null)
            {
                arguments = string.IsNullOrEmpty(arguments)
                    ? info.SilentSuffixArguments
                    : $"{arguments} {info.SilentSuffixArguments}";
            }

            isSilent = true;
        }

        return new InstallCommand
        {
            FileName = fileName,
            Arguments = arguments,
            RequiresElevation = app.Scope == InstallScope.Machine,
            IsSilent = isSilent,
        };
    }
}
