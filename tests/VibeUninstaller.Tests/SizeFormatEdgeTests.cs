using VibeUninstaller.Core.Reporting;

namespace VibeUninstaller.Tests;

/// <summary>Unit promotion and rounding boundaries of the size formatter.</summary>
public class SizeFormatEdgeTests
{
    [Theory]
    [InlineData(null, "-")]
    [InlineData(-1L, "-")]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "2 KB")]
    [InlineData(2560L, "2 KB")]
    [InlineData(1048575L, "1.0 MB")]
    [InlineData(1073741824L, "1.0 GB")]
    [InlineData(1099511627776L, "1.0 TB")]
    public void Bytes_formats_units_and_rounding(long? bytes, string expected) =>
        Assert.Equal(expected, SizeFormat.Bytes(bytes));

    [Fact]
    public void Rounding_never_produces_a_thousand_and_twenty_four_of_a_unit()
    {
        // 1048575 bytes is "1023.999 KB"; formatting it as "1,024 KB" would be
        // inconsistent, so the formatter promotes the unit instead.
        Assert.Equal("1.0 MB", SizeFormat.Bytes(1_048_575));
    }

    [Fact]
    public void Values_below_the_promotion_point_stay_in_their_unit()
    {
        Assert.Equal("1,023 KB", SizeFormat.Bytes(1_048_063));
    }
}
