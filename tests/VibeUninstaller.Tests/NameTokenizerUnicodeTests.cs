using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

/// <summary>Unicode, symbol and version-like token boundaries in the tokenizer.</summary>
public class NameTokenizerUnicodeTests
{
    [Fact]
    public void Non_ascii_letters_are_kept()
    {
        Assert.Equal(new[] { "zürich" }, NameTokenizer.Tokenize("Zürich Tools"));
        Assert.Equal(new[] { "münchen" }, NameTokenizer.Tokenize("MÜNCHEN"));
        Assert.Equal(new[] { "微信电脑版" }, NameTokenizer.Tokenize("微信电脑版"));
    }

    [Fact]
    public void Symbol_only_and_version_only_names_have_no_tokens()
    {
        Assert.Empty(NameTokenizer.Tokenize("C++"));
        Assert.Empty(NameTokenizer.Tokenize("+++"));
        Assert.Empty(NameTokenizer.Tokenize("v2"));
        Assert.Empty(NameTokenizer.Tokenize("24.09"));
        Assert.Empty(NameTokenizer.Tokenize("12345"));
    }

    [Fact]
    public void Versions_bitness_and_editions_are_stripped_from_real_names()
    {
        Assert.Equal(new[] { "zulu" }, NameTokenizer.Tokenize("Zulu 24.09 (x64)"));
        Assert.Equal(new[] { "zip" }, NameTokenizer.Tokenize("7-Zip"));
    }

    [Fact]
    public void Strong_and_weak_tokens_follow_their_sets()
    {
        Assert.True(NameTokenizer.IsStrongToken("zip"));
        Assert.False(NameTokenizer.IsStrongToken("bin"));
        Assert.False(NameTokenizer.IsStrongToken(string.Empty));
        Assert.True(NameTokenizer.IsWeakToken("bin"));
        Assert.False(NameTokenizer.IsWeakToken("zip"));
        Assert.False(NameTokenizer.IsWeakToken(string.Empty));
    }

    [Fact]
    public void Publisher_tokens_drop_legal_suffixes_but_never_return_nothing_for_a_real_name()
    {
        Assert.Equal(new[] { "contoso" }, NameTokenizer.TokenizePublisher("Contoso Ltd"));
        Assert.Equal(new[] { "igor", "pavlov" }, NameTokenizer.TokenizePublisher("Igor Pavlov"));

        // Every word of "The Company" is a stop word or legal suffix; the raw
        // tokens still survive so evidence is not silently lost.
        Assert.Equal(new[] { "company" }, NameTokenizer.TokenizePublisher("The Company"));
    }
}
