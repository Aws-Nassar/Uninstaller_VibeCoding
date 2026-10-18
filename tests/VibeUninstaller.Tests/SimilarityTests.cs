using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

/// <summary>Small similarity helpers: coverage, Jaccard and character trigrams.</summary>
public class SimilarityTests
{
    [Fact]
    public void Coverage_counts_every_candidate_token_even_when_repeated()
    {
        Assert.Equal(1.0, Similarity.Coverage(new[] { "a", "a" }, new HashSet<string> { "a" }));
        Assert.Equal(0.5, Similarity.Coverage(new[] { "a", "b" }, new HashSet<string> { "a" }));
    }

    [Fact]
    public void Coverage_is_zero_for_empty_inputs()
    {
        Assert.Equal(0.0, Similarity.Coverage(Array.Empty<string>(), new HashSet<string> { "a" }));
        Assert.Equal(0.0, Similarity.Coverage(new[] { "a" }, new HashSet<string>()));
    }

    [Fact]
    public void Jaccard_measures_overlap_over_union()
    {
        Assert.Equal(1.0, Similarity.Jaccard(new HashSet<string> { "a", "b" }, new HashSet<string> { "a", "b" }));
        Assert.Equal(1.0 / 3, Similarity.Jaccard(new HashSet<string> { "a", "b" }, new HashSet<string> { "b", "c" }), 10);
        Assert.Equal(0.0, Similarity.Jaccard(new HashSet<string> { "a" }, new HashSet<string> { "b" }));
        Assert.Equal(0.0, Similarity.Jaccard(new HashSet<string>(), new HashSet<string> { "a" }));
    }

    [Fact]
    public void Trigram_ignores_case_and_punctuation()
    {
        Assert.Equal(1.0, Similarity.Trigram("7-Zip", "7zip"));
        Assert.Equal(1.0, Similarity.Trigram("Visual Studio", "visualstudio"));
    }

    [Fact]
    public void Trigram_is_an_overlap_measure_not_a_substring_test()
    {
        // abcdef = {abc, bcd, cde, def}, abcxyz = {abc, bcx, cxy, xyz}.
        Assert.Equal(1.0 / 7, Similarity.Trigram("abcdef", "abcxyz"), 10);
    }

    [Fact]
    public void Trigram_handles_short_and_empty_inputs()
    {
        Assert.Equal(1.0, Similarity.Trigram("ab", "ab"));
        Assert.Equal(0.0, Similarity.Trigram("ab", "ba"));
        Assert.Equal(0.0, Similarity.Trigram("!!!", "???"));
        Assert.Equal(0.0, Similarity.Trigram(null, "abc"));
        Assert.Equal(0.0, Similarity.Trigram(string.Empty, "abc"));
        Assert.Equal(0.0, Similarity.Trigram("abc", string.Empty));
    }
}
