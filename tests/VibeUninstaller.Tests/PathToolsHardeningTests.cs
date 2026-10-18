using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

/// <summary>
/// Hardening tests: path tricks that could slip a candidate past prefix based
/// safety checks (extended-length prefixes, "." and ".." segments, trailing
/// dots/spaces) and the guid helpers used by the MSI evidence.
/// </summary>
public class PathToolsHardeningTests
{
    [Fact]
    public void Extended_length_prefixes_are_removed()
    {
        Assert.Equal(@"C:\Windows\System32\notepad.exe", PathTools.Normalize(@"\\?\C:\Windows\System32\notepad.exe"));
        Assert.Equal(@"C:\Windows", PathTools.Normalize(@"\\?\C:\Windows"));
    }

    [Fact]
    public void Extended_length_UNC_prefixes_are_removed()
    {
        Assert.Equal(@"\\server\share\7-Zip", PathTools.Normalize(@"\\?\UNC\server\share\7-Zip"));
        Assert.Equal(@"\\server\share\7-Zip", PathTools.Normalize(@"\\?\unc\server\share\7-Zip"));
    }

    [Fact]
    public void Trailing_dots_and_spaces_are_ignored_like_win32_does()
    {
        Assert.Equal(@"C:\Windows", PathTools.Normalize(@"C:\Windows."));
        Assert.Equal(@"C:\Windows", PathTools.Normalize(@"C:\Windows..."));
        Assert.Equal(@"C:\Program Files\7-Zip", PathTools.Normalize(@"C:\Program Files\7-Zip. "));
    }

    [Fact]
    public void Dot_segments_are_resolved()
    {
        Assert.Equal(@"C:\Windows\System32", PathTools.Normalize(@"C:\Windows.\System32"));
        Assert.Equal(@"C:\Windows\System32", PathTools.Normalize(@"C:\Windows\.\System32"));
    }

    [Fact]
    public void Parent_segments_are_resolved()
    {
        Assert.Equal(@"C:\Windows\notepad.exe", PathTools.Normalize(@"C:\Windows\System32\..\notepad.exe"));
        Assert.Equal(@"C:\Windows", PathTools.Normalize(@"C:\Windows\System32\..\Framework\.."));
    }

    [Fact]
    public void Parent_segments_cannot_escape_the_root()
    {
        Assert.Equal(@"C:\Windows", PathTools.Normalize(@"C:\..\Windows"));
        Assert.Equal(@"C:\", PathTools.Normalize(@"C:\.."));
        Assert.Equal(@"\\server\share\Other", PathTools.Normalize(@"\\server\share\App\..\Other"));
        Assert.Equal(@"\\server\share", PathTools.Normalize(@"\\server\share\App\.."));
    }

    [Fact]
    public void Duplicate_separators_are_collapsed_even_on_unc_paths()
    {
        Assert.Equal(@"C:\A\B", PathTools.Normalize(@"C:\A\\B"));
        Assert.Equal(@"\\server\share\dir", PathTools.Normalize(@"\\server\share\\dir"));
    }

    [Fact]
    public void Forward_slashes_and_trailing_separators_are_removed()
    {
        Assert.Equal(@"C:\Program Files\App", PathTools.Normalize("C:/Program Files//App/"));
        Assert.Equal(@"C:\", PathTools.Normalize(@"C:\."));
    }

    [Fact]
    public void Quotes_are_only_trimmed_at_the_ends()
    {
        Assert.Equal(@"C:\App", PathTools.Normalize("\"C:\\App\""));
        Assert.Equal("C:\\App\" trailing", PathTools.Normalize("\"C:\\App\" trailing"));
    }

    [Fact]
    public void IsUnder_matches_drive_rooted_paths_inside_the_root()
    {
        Assert.True(PathTools.IsUnder(@"C:\Windows\System32", @"C:\Windows"));
        Assert.True(PathTools.IsUnder(@"C:\Windows\System32", @"C:\Windows\"));
        Assert.True(PathTools.IsUnder(@"C:\Windows", "C:"));
        Assert.False(PathTools.IsUnder(@"C:\Windows", @"C:\Windows"));
        Assert.True(PathTools.IsUnder(@"C:\Windows", @"C:\Windows", includeEqual: true));
        Assert.False(PathTools.IsUnder(@"C:\WindowsX", @"C:\Windows"));
    }

    [Fact]
    public void IsUnder_needs_normalized_separators()
    {
        // The helper compares raw strings; "C:/App" style paths are only safe
        // after PathTools.Normalize has run.
        Assert.False(PathTools.IsUnder("C:/App/x", "C:/App"));
        Assert.True(PathTools.IsUnder(PathTools.Normalize("C:/App/x"), PathTools.Normalize("C:/App")));
    }

    [Fact]
    public void Packed_product_codes_reject_non_hex_input()
    {
        Assert.Equal(
            "F4FEF7635723E774F83A8C6AC13EE45D",
            PathTools.ToPackedProductCode("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}"));
        Assert.Null(PathTools.ToPackedProductCode("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}extra"));
        Assert.Null(PathTools.ToPackedProductCode("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz"));
        Assert.Null(PathTools.ToPackedProductCode(null));
    }

    [Fact]
    public void Guid_detection_requires_balanced_braces()
    {
        Assert.True(PathTools.LooksLikeGuid("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}"));
        Assert.True(PathTools.LooksLikeGuid("367FEF4F-3275-477E-8FA3-C8A61CE34ED5"));
        Assert.False(PathTools.LooksLikeGuid("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5"));
        Assert.False(PathTools.LooksLikeGuid("367FEF4F-3275-477E-8FA3-C8A61CE34ED5}"));
        Assert.False(PathTools.LooksLikeGuid("{not-a-guid}"));
        Assert.False(PathTools.LooksLikeGuid(null));
    }
}
