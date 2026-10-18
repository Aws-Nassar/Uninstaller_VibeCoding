using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// Detection precedence, silent switch building and the unusual families
/// (Squirrel, InstallShield, Steam) in the installer detector.
/// </summary>
public class InstallerInfoEdgeTests
{
    private static InstallerInfo Detect(InstalledApp app) =>
        InstallerInfoDetector.Detect(app, CommandLineSplitter.Split(app.UninstallString));

    [Fact]
    public void An_msi_product_code_beats_an_nsis_looking_uninstaller_name()
    {
        var app = TestData.SevenZip() with { ProductCode = "{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}" };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.Msi, info.Family);
        Assert.Equal($"/x {app.ProductCode} /qn /norestart", info.SilentArguments);
    }

    [Fact]
    public void Msiexec_without_a_product_code_has_no_silent_switch()
    {
        var app = TestData.SevenZip() with { UninstallString = "MsiExec.exe /qb" };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.Msi, info.Family);
        Assert.Null(info.SilentArguments);
        Assert.False(info.SupportsSilent);
        Assert.NotNull(info.Note);
        Assert.Contains("unknown", info.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_product_code_hidden_in_the_command_line_is_used()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"MsiExec.exe /X{367FEF4F-3275-477E-8FA3-C8A61CE34ED5} /qb",
        };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.Msi, info.Family);
        Assert.Equal("/x {367FEF4F-3275-477E-8FA3-C8A61CE34ED5} /qn /norestart", info.SilentArguments);
    }

    [Fact]
    public void Inno_and_nsis_uninstaller_names_do_not_collide()
    {
        Assert.Equal(
            InstallerFamily.InnoSetup,
            Detect(TestData.SevenZip() with { UninstallString = @"""C:\App\unins000.exe""" }).Family);
        Assert.Equal(
            InstallerFamily.Nsis,
            Detect(TestData.SevenZip() with { UninstallString = @"""C:\App\uninstaller.exe""" }).Family);

        // "unins.exe" matches neither pattern; it must stay Unknown rather than
        // guess a family, because wrong silent switches are worse than none.
        Assert.Equal(
            InstallerFamily.Unknown,
            Detect(TestData.SevenZip() with { UninstallString = @"""C:\App\unins.exe""" }).Family);
    }

    [Fact]
    public void Squirrel_is_recognized_by_switches_not_only_by_the_name()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"""C:\Users\tester\AppData\Local\Contoso\Update.exe"" --uninstaller-mode",
        };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.Squirrel, info.Family);
        Assert.Equal("--uninstall -s", info.SilentArguments);
    }

    [Fact]
    public void A_whitespace_only_quiet_string_falls_back_and_keeps_silent_switches()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"""C:\App\unins000.exe""",
            QuietUninstallString = "   ",
        };

        var info = Detect(app);
        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);

        Assert.True(command.IsSilent);
        Assert.Equal("/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", command.Arguments);
        Assert.Equal(@"C:\App\unins000.exe", command.FileName);
    }

    [Fact]
    public void Nsis_suffix_is_appended_to_existing_arguments()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString = @"""C:\App\Uninstall.exe"" --mode=uninstall",
            QuietUninstallString = null,
        };

        var info = Detect(app);
        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);

        Assert.Equal(InstallerFamily.Nsis, info.Family);
        Assert.Equal("--mode=uninstall /S", command.Arguments);
    }

    [Fact]
    public void InstallShield_is_detected_but_has_no_silent_switch_yet()
    {
        var app = TestData.SevenZip() with
        {
            UninstallString =
                @"""C:\Program Files\InstallShield Installation Information\{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}\setup.exe"" -runfromtemp",
        };

        var info = Detect(app);

        Assert.Equal(InstallerFamily.InstallShield, info.Family);
        Assert.False(info.SupportsSilent);
        Assert.NotNull(info.Note);
    }

    [Fact]
    public void Steam_apps_run_through_the_steam_protocol()
    {
        var app = TestData.SevenZip() with
        {
            Source = AppSource.Steam,
            SteamAppId = "440",
            UninstallString = null,
        };

        var info = Detect(app);
        var command = InstallerInfoDetector.BuildCommand(app, info, preferSilent: true);

        Assert.Equal(InstallerFamily.Steam, info.Family);
        Assert.Equal("steam://uninstall/440", command.FileName);
        Assert.False(command.RequiresElevation);

        var withString = app with { UninstallString = "steam://uninstall/440" };
        var commandFromString = InstallerInfoDetector.BuildCommand(withString, info, preferSilent: true);
        Assert.Equal("steam://uninstall/440", commandFromString.FileName);
    }
}
