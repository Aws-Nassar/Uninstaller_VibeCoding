using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

public class NameTokenizerTests
{
    [Theory]
    [InlineData("7-Zip 24.09 (x64)", new[] { "zip" })]
    [InlineData("VLC media player", new[] { "vlc", "media", "player" })]
    [InlineData("Notepad++", new[] { "notepad" })]
    [InlineData("Microsoft Visual Studio Code", new[] { "microsoft", "visual", "studio", "code" })]
    [InlineData("Google Chrome", new[] { "google", "chrome" })]
    [InlineData("App Installer", new string[0])]
    public void Tokenize_drops_versions_editions_and_bitness(string name, string[] expected)
    {
        Assert.Equal(expected, NameTokenizer.Tokenize(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2024")]
    public void Tokenize_returns_nothing_for_empty_or_numeric_names(string? name)
    {
        Assert.Empty(NameTokenizer.Tokenize(name));
    }

    [Theory]
    [InlineData("Contoso Ltd", new[] { "contoso" })]
    [InlineData("Igor Pavlov", new[] { "igor", "pavlov" })]
    [InlineData("InterPromo GMBH", new[] { "interpromo" })]
    public void TokenizePublisher_strips_legal_suffixes(string publisher, string[] expected)
    {
        Assert.Equal(expected, NameTokenizer.TokenizePublisher(publisher));
    }

    [Fact]
    public void TokenizePublisher_keeps_the_original_when_everything_is_a_legal_suffix()
    {
        Assert.Equal(new[] { "company" }, NameTokenizer.TokenizePublisher("The Company"));
    }

    [Theory]
    [InlineData("chrome", true)]
    [InlineData("doc", false)]
    [InlineData("media", false)]
    [InlineData("plugins", false)]
    public void Strong_tokens_drive_confidence(string token, bool expected)
    {
        Assert.Equal(expected, NameTokenizer.IsStrongToken(token));
        Assert.Equal(!expected, NameTokenizer.IsWeakToken(token));
    }
}
