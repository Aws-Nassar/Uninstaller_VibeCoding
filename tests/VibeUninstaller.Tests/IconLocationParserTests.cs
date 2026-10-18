using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

/// <summary>
/// DisplayIcon values come straight from arbitrary installers, so the parser
/// must survive every shape seen in the wild without throwing.
/// </summary>
public class IconLocationParserTests
{
    [Theory]
    [InlineData(@"C:\App\app.exe", @"C:\App\app.exe", 0)]
    [InlineData(@"C:\App\app.exe,0", @"C:\App\app.exe", 0)]
    [InlineData(@"C:\App\app.exe,3", @"C:\App\app.exe", 3)]
    [InlineData(@"C:\App\app.exe,-101", @"C:\App\app.exe", -101)]
    [InlineData("\"C:\\Program Files\\App\\app.exe\",1", @"C:\Program Files\App\app.exe", 1)]
    [InlineData("  C:\\App\\app.exe , 2 ", @"C:\App\app.exe", 2)]
    [InlineData(@"%ProgramFiles%\App\app.exe,0", @"%ProgramFiles%\App\app.exe", 0)]
    [InlineData(@"C:\App\a,b\icon.exe", @"C:\App\a,b\icon.exe", 0)]
    [InlineData(@"""C:\App\app.exe""", @"C:\App\app.exe", 0)]
    public void Parses_real_world_display_icon_values(string input, string expectedPath, int expectedIndex)
    {
        var reference = IconLocationParser.Parse(input);

        Assert.NotNull(reference);
        Assert.Equal(expectedPath, reference!.Path);
        Assert.Equal(expectedIndex, reference.Index);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData(",")]
    public void Rejects_empty_values(string? input) => Assert.Null(IconLocationParser.Parse(input));

    [Fact]
    public void Rejects_paths_containing_invalid_characters() =>
        Assert.Null(IconLocationParser.Parse(@"C:\App\bad|name.exe,0"));

    [Fact]
    public void Keeps_a_trailing_non_numeric_suffix_as_part_of_the_path()
    {
        var reference = IconLocationParser.Parse(@"C:\App\v1,2\app.exe");

        Assert.NotNull(reference);
        Assert.Equal(@"C:\App\v1,2\app.exe", reference!.Path);
        Assert.Equal(0, reference.Index);
    }
}
