using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// A synthetic Windows installation. Every piece of engine logic under test is
/// pure, so the tests never touch the real file system or registry.
/// </summary>
internal static class TestData
{
    public const string ProgramFiles = @"C:\Program Files";

    public const string ProgramFilesX86 = @"C:\Program Files (x86)";

    public const string ProgramData = @"C:\ProgramData";

    public const string LocalAppData = @"C:\Users\tester\AppData\Local";

    public const string RoamingAppData = @"C:\Users\tester\AppData\Roaming";

    public const string Temp = @"C:\Users\tester\AppData\Local\Temp";

    public static FingerprintEnvironment Environment() => new()
    {
        WindowsDirectory = @"C:\Windows",
        ProgramFiles = ProgramFiles,
        ProgramFilesX86 = ProgramFilesX86,
        ProgramData = ProgramData,
        LocalAppData = LocalAppData,
        LocalAppDataLow = @"C:\Users\tester\AppData\LocalLow",
        RoamingAppData = RoamingAppData,
        UserProfile = @"C:\Users\tester",
        UserDesktop = @"C:\Users\tester\Desktop",
        PublicDesktop = @"C:\Users\Public\Desktop",
        Public = @"C:\Users\Public",
        Temp = Temp,
        SystemDrive = @"C:\",
    };

    public static LeftoverRules Rules() => LeftoverRules.CreateDefault(Environment());

    public static InstalledApp SevenZip() => new()
    {
        Id = @"reg|HKLM|7-Zip",
        DisplayName = "7-Zip 24.09 (x64)",
        Version = "24.09",
        Publisher = "Igor Pavlov",
        InstallLocation = @"C:\Program Files\7-Zip",
        UninstallString = @"""C:\Program Files\7-Zip\Uninstall.exe""",
        QuietUninstallString = @"""C:\Program Files\7-Zip\Uninstall.exe"" /S",
        RegistryKeyPath = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\7-Zip",
        Source = AppSource.Registry,
        Scope = InstallScope.Machine,
    };

    public static InstalledApp MsiApp(string productCode = "{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}") => new()
    {
        Id = "reg|HKLM|Contoso Suite",
        DisplayName = "Contoso Suite",
        Publisher = "Contoso Ltd",
        ProductCode = productCode,
        UninstallString = $@"MsiExec.exe /X{productCode}",
        Source = AppSource.Registry,
        Scope = InstallScope.Machine,
    };

    public static AppFingerprint Fingerprint(InstalledApp app, IReadOnlyCollection<string>? files = null) =>
        FingerprintBuilder.From(app, Environment(), files);

    public static LeftoverClassifier Classifier(InstalledApp app, IReadOnlyCollection<string>? files = null) =>
        new(Fingerprint(app, files), Rules(), Environment());
}
