namespace VibeUninstaller.Core.Analysis;

/// <summary>Small similarity helpers used by the leftover classifier.</summary>
public static class Similarity
{
    /// <summary>Fraction of <paramref name="candidateTokens"/> that also occur in <paramref name="reference"/>.</summary>
    public static double Coverage(IReadOnlyCollection<string> candidateTokens, IReadOnlySet<string> reference)
    {
        if (candidateTokens.Count == 0 || reference.Count == 0)
        {
            return 0;
        }

        var hits = 0;
        foreach (var token in candidateTokens)
        {
            if (reference.Contains(token))
            {
                hits++;
            }
        }

        return (double)hits / candidateTokens.Count;
    }

    /// <summary>Jaccard similarity between two token sets.</summary>
    public static double Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = 0;
        foreach (var token in left)
        {
            if (right.Contains(token))
            {
                intersection++;
            }
        }

        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>Character trigram similarity, useful for names that differ by punctuation.</summary>
    public static double Trigram(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return 0;
        }

        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var a = Trigrams(left);
        var b = Trigrams(right);
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var intersection = 0;
        foreach (var trigram in a)
        {
            if (b.Contains(trigram))
            {
                intersection++;
            }
        }

        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;

        static HashSet<string> Trigrams(string value)
        {
            var normalized = new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i + 3 <= normalized.Length; i++)
            {
                result.Add(normalized.Substring(i, 3));
            }

            if (result.Count == 0 && normalized.Length > 0)
            {
                result.Add(normalized);
            }

            return result;
        }
    }
}
