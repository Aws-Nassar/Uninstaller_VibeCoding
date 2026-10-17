using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

public class PathToolsTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\App""", @"C:\Program Files\App")]
    [InlineData("C:/Program Files/App/", @"C:\Program Files\App")]
    [InlineData(@"C:\Program Files\App\\\", @"C:\Program Files\App")]
    [InlineData(@"  C:\Program Files\App  ", @"C:\Program Files\App")]
    [InlineData(@"C:\\Program Files\\App", @"C:\Program Files\App")]
    [InlineData("C:", @"C:\")]
    public void Normalize_cleans_paths(string input, string expected)
    {
        Assert.Equal(expected, PathTools.Normalize(input));
    }

    [Fact]
    public void Normalize_returns_empty_for_null_or_blank()
    {
        Assert.Equal(string.Empty, PathTools.Normalize(null));
        Assert.Equal(string.Empty, PathTools.Normalize("   "));
    }

    [Fact]
    public void Normalize_expands_environment_variables()
    {
        Environment.SetEnvironmentVariable("VIBE_TEST_ROOT", @"D:\VibeTest");
        try
        {
            Assert.Equal(@"D:\VibeTest\folder", PathTools.Normalize(@"%VIBE_TEST_ROOT%\folder/"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIBE_TEST_ROOT", null);
        }
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\sub", @"C:\Program Files\App")]
    [InlineData(@"C:\Program Files\App\sub\deep", @"C:\Program Files\App")]
    public void IsUnder_matches_real_descendants(string candidate, string root)
    {
        Assert.True(PathTools.IsUnder(candidate, root));
    }

    [Theory]
    [InlineData(@"C:\Program Files\App", @"C:\Program Files\App")]
    [InlineData(@"C:\Program Files\App2", @"C:\Program Files\App")]
    [InlineData(@"C:\Program Files\App", @"C:\Program Files\App\sub")]
    [InlineData(@"C:\Program", @"C:\Program Files")]
    [InlineData("", @"C:\Program Files")]
    public void IsUnder_rejects_everything_else(string candidate, string root)
    {
        Assert.False(PathTools.IsUnder(candidate, root));
    }

    [Fact]
    public void IsUnder_can_include_the_root_itself()
    {
        Assert.True(PathTools.IsUnder(@"C:\Temp", @"C:\Temp", includeEqual: true));
        Assert.False(PathTools.IsUnder(@"C:\Temp", @"C:\Temp"));
    }

    [Theory]
    [InlineData(@"C:\A\B\C", "C")]
    [InlineData(@"C:\A\B\", "B")]
    [InlineData("", "")]
    public void LastSegment_returns_the_final_element(string path, string expected)
    {
        Assert.Equal(expected, PathTools.LastSegment(path));
    }

    [Theory]
    [InlineData(@"C:\A\B\C", @"C:\A\B")]
    [InlineData(@"C:\A\B", @"C:\A")]
    [InlineData(@"C:\A", @"C:\")]
    [InlineData(@"C:\", "")]
    public void Parent_walks_up_without_losing_the_drive_root(string path, string expected)
    {
        Assert.Equal(expected, PathTools.Parent(path));
    }

    [Fact]
    public void SplitSegments_ignores_the_drive_root()
    {
        Assert.Equal(new[] { "C:", "Program Files", "App" }, PathTools.SplitSegments(@"C:\Program Files\App"));
        Assert.Empty(PathTools.SplitSegments(null));
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\App", "HKLM", @"SOFTWARE\App")]
    [InlineData(@"HKCU\SOFTWARE", "HKCU", "SOFTWARE")]
    [InlineData(@"HKCR\CLSID", "HKCR", "CLSID")]
    public void SplitRegistryPath_recognizes_hives(string path, string hive, string subPath)
    {
        var split = PathTools.SplitRegistryPath(path);

        Assert.NotNull(split);
        Assert.Equal(hive, split!.Value.Hive);
        Assert.Equal(subPath, split.Value.SubPath);
    }

    [Theory]
    [InlineData("SOFTWARE\\App")]
    [InlineData(@"ZZZZ\SOFTWARE")]
    [InlineData("")]
    public void SplitRegistryPath_rejects_unknown_hives(string path)
    {
        Assert.Null(PathTools.SplitRegistryPath(path));
    }

    [Fact]
    public void ToPackedProductCode_reverses_the_first_three_groups()
    {
        Assert.Equal("F4FEF7635723E774F83A8C6AC13EE45D", PathTools.ToPackedProductCode("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}"));
        Assert.Equal("F4FEF7635723E774F83A8C6AC13EE45D", PathTools.ToPackedProductCode("367FEF4F-3275-477E-8FA3-C8A61CE34ED5"));
        Assert.Null(PathTools.ToPackedProductCode("not-a-guid"));
    }

    [Theory]
    [InlineData("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}", true)]
    [InlineData("367FEF4F-3275-477E-8FA3-C8A61CE34ED5", true)]
    [InlineData("{367FEF4F-3275-477E-8FA3}", false)]
    [InlineData("", false)]
    public void LooksLikeGuid_validates_product_codes(string value, bool expected)
    {
        Assert.Equal(expected, PathTools.LooksLikeGuid(value));
    }
}
