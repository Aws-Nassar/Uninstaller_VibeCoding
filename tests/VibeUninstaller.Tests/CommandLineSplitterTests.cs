using VibeUninstaller.Core.Execution;

namespace VibeUninstaller.Tests;

public class CommandLineSplitterTests
{
    [Fact]
    public void Split_handles_quoted_paths_with_spaces()
    {
        var parsed = CommandLineSplitter.Split(@"""C:\Program Files\App\unins000.exe"" /VERYSILENT /SUPPRESSMSGBOXES");

        Assert.NotNull(parsed);
        Assert.Equal(@"C:\Program Files\App\unins000.exe", parsed!.Value.FileName);
        Assert.Equal("/VERYSILENT /SUPPRESSMSGBOXES", parsed.Value.Arguments);
    }

    [Fact]
    public void Split_handles_unquoted_paths_with_spaces()
    {
        var parsed = CommandLineSplitter.Split(@"C:\Program Files\App\uninstall.exe /S");

        Assert.NotNull(parsed);
        Assert.Equal(@"C:\Program Files\App\uninstall.exe", parsed!.Value.FileName);
        Assert.Equal("/S", parsed.Value.Arguments);
    }

    [Fact]
    public void Split_handles_a_bare_command()
    {
        var parsed = CommandLineSplitter.Split(@"C:\App\uninstall.exe");

        Assert.NotNull(parsed);
        Assert.Equal(@"C:\App\uninstall.exe", parsed!.Value.FileName);
        Assert.Equal(string.Empty, parsed.Value.Arguments);
    }

    [Fact]
    public void Split_handles_msiexec_without_a_path()
    {
        var parsed = CommandLineSplitter.Split("MsiExec.exe /X{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}");

        Assert.NotNull(parsed);
        Assert.Equal("MsiExec.exe", parsed!.Value.FileName);
        Assert.Equal("/X{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}", parsed.Value.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_returns_null_for_empty_input(string? commandLine)
    {
        Assert.Null(CommandLineSplitter.Split(commandLine));
    }

    [Theory]
    [InlineData("msiexec.exe /x {367FEF4F-3275-477E-8FA3-C8A61CE34ED5} /qn", "{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}")]
    [InlineData("MsiExec.exe /I{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}", "{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}")]
    [InlineData("msiexec.exe /qb", null)]
    public void TryGetMsiProductCode_reads_the_product_code(string commandLine, string? expected)
    {
        Assert.Equal(expected, CommandLineSplitter.TryGetMsiProductCode(commandLine));
    }

    [Theory]
    [InlineData(@"C:\App\uninstall.exe /S", @"C:\App\uninstall.exe")]
    [InlineData("steam://uninstall/1234", null)]
    [InlineData("MsiExec.exe /X{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}", null)]
    public void TryGetExecutablePath_only_returns_real_paths(string commandLine, string? expected)
    {
        Assert.Equal(expected, CommandLineSplitter.TryGetExecutablePath(commandLine));
    }
}
