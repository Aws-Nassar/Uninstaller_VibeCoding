using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

public class LeftoverRulesTests
{
    private static readonly LeftoverRules Rules = TestData.Rules();

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Windows\System32\drivers\etc")]
    [InlineData(@"C:\Program Files\WindowsApps")]
    [InlineData(@"C:\ProgramData\Microsoft")]
    [InlineData(@"C:\Users\tester\AppData\Local\Microsoft")]
    [InlineData(@"C:\Users\tester\AppData\Local\Temp")]
    [InlineData(@"C:\$Recycle.Bin")]
    [InlineData(@"C:\System Volume Information")]
    public void Denied_file_locations_are_never_touched(string path)
    {
        Assert.True(Rules.IsFileDenied(path));
    }

    [Theory]
    [InlineData(@"C:\Program Files\7-Zip")]
    [InlineData(@"C:\ProgramData\7-Zip")]
    [InlineData(@"C:\Users\tester\AppData\Roaming\SomeApp")]
    public void Normal_application_locations_are_allowed(string path)
    {
        Assert.False(Rules.IsFileDenied(path));
    }

    [Fact]
    public void The_start_menu_is_allowed_even_though_it_lives_under_a_denied_folder()
    {
        const string shortcut = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk";

        Assert.False(Rules.IsFileDenied(shortcut));
        Assert.True(Rules.CanDescendIntoFile(@"C:\ProgramData\Microsoft"));
        Assert.True(Rules.CanDescendIntoFile(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs"));
    }

    [Fact]
    public void Junk_file_names_are_denied_everywhere()
    {
        Assert.True(Rules.IsFileDenied(@"C:\ProgramData\7-Zip\desktop.ini"));
        Assert.True(Rules.IsFileDenied(@"C:\ProgramData\7-Zip\Thumbs.db"));
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft")]
    [InlineData(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer")]
    [InlineData(@"HKLM\SOFTWARE\Policies")]
    [InlineData(@"HKLM\SOFTWARE\Classes\Installer\Products\F4FEF7635723E774F83A8C6AC13EE45D")]
    [InlineData(@"HKLM\SOFTWARE")]
    [InlineData(@"HKCU\SOFTWARE")]
    public void Protected_registry_branches_are_denied(string key)
    {
        Assert.True(Rules.IsRegistryDenied(key));
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\7-Zip")]
    [InlineData(@"HKCU\SOFTWARE\Contoso")]
    [InlineData(@"HKLM\SOFTWARE\WOW6432Node\7-Zip")]
    public void Application_registry_keys_are_allowed(string key)
    {
        Assert.False(Rules.IsRegistryDenied(key));
    }

    [Fact]
    public void Uninstall_keys_are_reachable_although_they_live_under_microsoft()
    {
        const string key = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\7-Zip";

        Assert.False(Rules.IsRegistryDenied(key));
        Assert.True(Rules.CanDescendIntoRegistry(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));
        Assert.False(Rules.CanDescendIntoRegistry(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"));
    }

    [Fact]
    public void The_deny_list_is_baked_into_code_not_configuration()
    {
        Assert.NotEmpty(Rules.FileDenyPrefixes);
        Assert.NotEmpty(Rules.RegistryDenyPrefixes);
        Assert.Contains(@"C:\Windows", Rules.FileDenyPrefixes);
    }
}
