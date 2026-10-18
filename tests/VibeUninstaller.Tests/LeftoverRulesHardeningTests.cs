using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

/// <summary>
/// The deny list must not depend on the caller having canonicalized the path.
/// These tests feed it the classic Win32 bypass spellings directly.
/// </summary>
public class LeftoverRulesHardeningTests
{
    private static readonly LeftoverRules Rules = TestData.Rules();

    [Fact]
    public void Extended_length_paths_into_denied_folders_are_denied()
    {
        Assert.True(Rules.IsFileDenied(@"\\?\C:\Windows\System32\evil.dll"));
        Assert.True(Rules.IsFileDenied(@"\\?\C:\Users\tester\AppData\Local\Temp\evil.tmp"));
    }

    [Fact]
    public void Dot_and_dotdot_tricks_into_denied_folders_are_denied()
    {
        Assert.True(Rules.IsFileDenied(@"C:\Windows.\System32\evil.dll"));
        Assert.True(Rules.IsFileDenied(@"C:\Windows\System32\..\evil.dll"));
        Assert.True(Rules.IsFileDenied(@"C:\ProgramData\Microsoft."));
    }

    [Fact]
    public void Denied_file_names_are_caught_after_normalization()
    {
        Assert.True(Rules.IsFileDenied(@"\\?\C:\Apps\desktop.ini"));
        Assert.True(Rules.IsFileDenied(@"C:\Apps\Desktop.INI"));
        Assert.False(Rules.IsFileDenied(@"C:\Apps\desktop.ini.bak"));
    }

    [Fact]
    public void The_start_menu_allow_list_still_wins_over_the_deny_list()
    {
        Assert.False(Rules.IsFileDenied(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip"));
        Assert.False(Rules.IsFileDenied(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip.lnk"));
        Assert.True(Rules.IsFileDenied(@"C:\ProgramData\Microsoft\Windows\Start MenuOther"));
    }

    [Fact]
    public void Capture_may_descend_towards_an_allowed_start_menu()
    {
        // The folder itself is denied for removal purposes...
        Assert.True(Rules.IsFileDenied(@"C:\ProgramData\Microsoft"));

        // ...but the walker must still be allowed to descend to the allow list.
        Assert.True(Rules.CanDescendIntoFile(@"C:\ProgramData\Microsoft"));
        Assert.False(Rules.CanDescendIntoFile(@"C:\Windows\System32"));
    }

    [Fact]
    public void Registry_paths_are_normalized_before_matching()
    {
        Assert.True(Rules.IsRegistryDenied(@"HKLM\SOFTWARE\Microsoft.\Windows\CurrentVersion\Run"));
        Assert.False(Rules.IsRegistryDenied(@"HKLM\SOFTWARE\Contoso"));
    }

    [Fact]
    public void Empty_paths_are_always_denied()
    {
        Assert.True(Rules.IsFileDenied(string.Empty));
        Assert.True(Rules.IsRegistryDenied(string.Empty));
    }
}
