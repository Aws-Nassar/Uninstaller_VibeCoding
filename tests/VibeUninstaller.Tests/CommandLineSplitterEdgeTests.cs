using VibeUninstaller.Core.Execution;

namespace VibeUninstaller.Tests;

/// <summary>
/// Quote handling, ".exe" detection and path extraction edge cases in the
/// uninstall command line parser.
/// </summary>
public class CommandLineSplitterEdgeTests
{
    [Fact]
    public void Empty_quoted_commands_are_rejected()
    {
        Assert.Null(CommandLineSplitter.Split("\"\""));
        Assert.Null(CommandLineSplitter.Split("\"   \""));
        Assert.Null(CommandLineSplitter.Split("   "));
        Assert.Null(CommandLineSplitter.Split(null));
    }

    [Fact]
    public void An_unbalanced_quote_still_yields_the_executable()
    {
        var split = CommandLineSplitter.Split("\"C:\\App\\uninstall.exe");

        Assert.NotNull(split);
        Assert.Equal(@"C:\App\uninstall.exe", split!.Value.FileName);
        Assert.Equal(string.Empty, split.Value.Arguments);
    }

    [Fact]
    public void Arguments_keep_their_own_quotes()
    {
        var split = CommandLineSplitter.Split("\"C:\\A\\u.exe\" \"/S\"");

        Assert.NotNull(split);
        Assert.Equal(@"C:\A\u.exe", split!.Value.FileName);
        Assert.Equal("\"/S\"", split.Value.Arguments);
    }

    [Fact]
    public void Executable_detection_is_case_insensitive_and_ignores_exe_in_folder_names()
    {
        var lower = CommandLineSplitter.Split(@"c:\app\uninstall.EXE /S");
        Assert.NotNull(lower);
        Assert.Equal(@"c:\app\uninstall.EXE", lower!.Value.FileName);
        Assert.Equal("/S", lower.Value.Arguments);

        var tricky = CommandLineSplitter.Split(@"C:\my.exe.tool\run.exe /S");
        Assert.NotNull(tricky);
        Assert.Equal(@"C:\my.exe.tool\run.exe", tricky!.Value.FileName);
        Assert.Equal("/S", tricky.Value.Arguments);
    }

    [Fact]
    public void Environment_variables_are_expanded_before_splitting()
    {
        var root = Environment.ExpandEnvironmentVariables("%SystemRoot%");
        var split = CommandLineSplitter.Split(@"%SystemRoot%\App\unins000.exe");

        Assert.NotNull(split);
        Assert.Equal($@"{root}\App\unins000.exe", split!.Value.FileName);
    }

    [Fact]
    public void Relative_and_protocol_commands_have_no_executable_path()
    {
        Assert.Null(CommandLineSplitter.TryGetExecutablePath("uninstall.exe"));
        Assert.Null(CommandLineSplitter.TryGetExecutablePath("notepad"));
        Assert.Null(CommandLineSplitter.TryGetExecutablePath("steam://uninstall/440"));
        Assert.Null(CommandLineSplitter.TryGetExecutablePath("rundll32.exe \"a.dll\",Uninstall"));
        Assert.Equal(@"C:\App\u.exe", CommandLineSplitter.TryGetExecutablePath(@"""C:\App\u.exe"" /S"));
    }

    [Fact]
    public void Msi_product_codes_are_read_from_uninstall_switches()
    {
        const string code = "{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}";

        Assert.Equal(code, CommandLineSplitter.TryGetMsiProductCode($@"MsiExec.exe /X{code} /qn"));
        Assert.Equal(code, CommandLineSplitter.TryGetMsiProductCode($@"msiexec.exe /I {code}"));
        Assert.Equal(code, CommandLineSplitter.TryGetMsiProductCode($@"MsiExec.exe /x""{code}"""));
        Assert.Null(CommandLineSplitter.TryGetMsiProductCode("MsiExec.exe /X367FEF4F-3275-477E-8FA3-C8A61CE34ED5"));
        Assert.Null(CommandLineSplitter.TryGetMsiProductCode(null));
    }
}
