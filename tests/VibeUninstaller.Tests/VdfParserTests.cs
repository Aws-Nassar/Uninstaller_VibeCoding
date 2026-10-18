using VibeUninstaller.Windows.Providers;

namespace VibeUninstaller.Tests;

/// <summary>
/// The minimal Valve KeyValues parser used for Steam libraries and app
/// manifests: quoting, escapes, nesting and malformed input.
/// </summary>
public class VdfParserTests
{
    [Fact]
    public void Parses_nested_objects_and_values()
    {
        const string text =
            @"""AppState"" { ""appid"" ""440"" ""name"" ""Team Fortress 2"" " +
            @"""UserConfig"" { ""Language"" ""english"" } }";

        var result = VdfParser.Parse(text);

        var appState = Assert.IsType<Dictionary<string, object>>(result["AppState"]);
        Assert.Equal("440", appState["appid"]);
        Assert.Equal("Team Fortress 2", appState["name"]);
        var userConfig = Assert.IsType<Dictionary<string, object>>(appState["UserConfig"]);
        Assert.Equal("english", userConfig["Language"]);
    }

    [Fact]
    public void Top_level_braces_are_optional()
    {
        Assert.Equal("1", VdfParser.Parse(@"""a"" ""1""")["a"]);
        Assert.Equal("1", VdfParser.Parse(@"{ ""a"" ""1"" }")["a"]);
    }

    [Fact]
    public void Keys_are_case_insensitive_and_the_last_value_wins()
    {
        var result = VdfParser.Parse(@"""name"" ""A"" ""NAME"" ""B""");

        Assert.Equal("B", result["name"]);
        Assert.Single(result);
    }

    [Fact]
    public void Unquoted_values_become_empty_strings()
    {
        var result = VdfParser.Parse(@"""key"" garbage");

        Assert.Equal(string.Empty, result["key"]);
    }

    [Fact]
    public void Escapes_are_unescaped()
    {
        var result = VdfParser.Parse(@"""path"" ""C:\\Games\\TF2"" ""description"" ""line1\nline2""");

        Assert.Equal(@"C:\Games\TF2", result["path"]);
        Assert.Equal("line1\nline2", result["description"]);
    }

    [Fact]
    public void Quotes_braces_and_slashes_inside_values_are_kept()
    {
        var result = VdfParser.Parse(@"""url"" ""steam://store/440"" ""title"" ""weird {value} // not a comment""");

        Assert.Equal("steam://store/440", result["url"]);
        Assert.Equal("weird {value} // not a comment", result["title"]);
    }

    [Fact]
    public void Comments_outside_values_are_skipped()
    {
        var result = VdfParser.Parse("// header comment\n\"a\" \"1\" // trailing\n");

        Assert.Equal("1", result["a"]);
        Assert.Single(result);
    }

    [Fact]
    public void Empty_and_null_text_produce_an_empty_dictionary()
    {
        Assert.Empty(VdfParser.Parse(null));
        Assert.Empty(VdfParser.Parse(string.Empty));
        Assert.Empty(VdfParser.Parse("{ }"));
    }

    [Fact]
    public void An_unterminated_quote_reads_to_the_end_instead_of_crashing()
    {
        var result = VdfParser.Parse(@"""a"" ""unterminated");

        Assert.Equal("unterminated", result["a"]);
    }
}
