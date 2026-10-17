using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

public class InstallerInfoDetectorTests
{
    private static InstallerInfo Detect(InstalledApp app) =>
        InstallerInfoDetector.Detect(app, CommandLineSplitter.Split(app.UninstallString));

    [Fact]
    public void Msi_is_recognized_from_the_product_code()
    {
        var app = TestData.MsiApp();

        Assert.Equal(InstallerFamily.Msi, Detect(app).Family);
    }

    [Fact]
    public void Msi_silent_command_uninstalls_by_product_code()
    {
        var app = TestData.MsiApp();
        var info = Detect(app);
        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);

        Assert.True(command.IsSilent);
        Assert.Equal("/x {367FEF4F-3275-477E-8FA3-C8A61CE34ED5} /qn /norestart", command.Arguments);
    }

    [Fact]
    public void Inno_setup_is_recognized_and_gets_silent_switches()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"""C:\Program Files\App\unins000.exe""",
            QuietUninstallString = null,
        };
        var info = Detect(app);

        Assert.Equal(InstallerFamily.InnoSetup, info.Family);

        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);
        Assert.True(command.IsSilent);
        Assert.Contains("/VERYSILENT", command.Arguments);
    }

    [Fact]
    public void Nsis_is_recognized_by_the_uninstaller_name()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"C:\Program Files\App\Uninstall.exe",
            QuietUninstallString = null,
        };
        var info = Detect(app);

        Assert.Equal(InstallerFamily.Nsis, info.Family);

        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);
        Assert.Equal("/S", command.Arguments);
    }

    [Fact]
    public void Inno_and_nsis_uninstallers_are_not_confused()
    {
        var inno = TestData.SevenZip() with { UninstallString = @"C:\App\unins001.exe" };
        var nsis = TestData.SevenZip() with { UninstallString = @"C:\App\uninstall.exe" };

        Assert.Equal(InstallerFamily.InnoSetup, Detect(inno).Family);
        Assert.Equal(InstallerFamily.Nsis, Detect(nsis).Family);
    }

    [Fact]
    public void Squirrel_is_recognized_from_the_update_launcher()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"C:\Users\tester\AppData\Local\App\Update.exe --uninstall",
        };

        Assert.Equal(InstallerFamily.Squirrel, Detect(app).Family);
    }

    [Fact]
    public void Store_packages_are_handled_by_the_package_manager()
    {
        var app = new InstalledApp
        {
            Id = "appx|Contoso.App",
            DisplayName = "Contoso App",
            Source = AppSource.Appx,
            Scope = InstallScope.User,
        };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.Appx, info.Family);
        Assert.False(info.SupportsSilent);
    }

    [Fact]
    public void Unknown_installers_keep_the_original_command()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"""C:\Program Files\App\custom-uninst.exe"" --go",
            QuietUninstallString = null,
        };
        var info = Detect(app);

        Assert.Equal(InstallerFamily.Unknown, info.Family);

        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);
        Assert.Equal(@"C:\Program Files\App\custom-uninst.exe", command.FileName);
        Assert.Equal("--go", command.Arguments);
        Assert.False(command.IsSilent);
    }

    [Fact]
    public void Machine_wide_apps_require_elevation()
    {
        var app = TestData.SevenZip();
        var command = InstallerInfoDetector.BuildCommand(app, Detect(app), preferSilent: false);

        Assert.True(command.RequiresElevation);
    }

    [Fact]
    public void A_quiet_uninstall_string_is_preferred_for_silent_runs()
    {
        var app = TestData.SevenZip();
        var info = Detect(app);

        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);

        Assert.True(command.IsSilent);
        Assert.Equal(@"C:\Program Files\7-Zip\Uninstall.exe", command.FileName);
        Assert.Equal("/S", command.Arguments);
    }

    [Fact]
    public void Steam_games_are_removed_through_the_client()
    {
        var app = new InstalledApp
        {
            Id = "steam|440",
            DisplayName = "Team Fortress 2",
            Source = AppSource.Steam,
            Scope = InstallScope.Machine,
            SteamAppId = "440",
            InstallLocation = @"D:\Steam\steamapps\common\Team Fortress 2",
        };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.Steam, info.Family);

        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: false);
        Assert.Equal("steam://uninstall/440", command.FileName);
        Assert.False(command.RequiresElevation);
    }
}
