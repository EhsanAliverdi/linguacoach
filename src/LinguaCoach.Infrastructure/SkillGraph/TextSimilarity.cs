namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>Shared bigram-similarity helper, extracted from
/// <see cref="GraphChangeSuggestionService"/> (2026-07-24) so the CEFR-J importer's fuzzy container
/// matching can reuse the same scoring instead of re-implementing it.</summary>
public static class TextSimilarity
{
    /// <summary>Sorensen-Dice coefficient over character bigrams, case-insensitive, counting
    /// repeated bigrams (a multiset intersection, not a plain set intersection — "aabb" vs "aabb"
    /// must score 1.0). Returns 1.0 for identical strings, 0.0 when either string is too short to
    /// form a bigram (or empty) and the strings aren't identical.</summary>
    public static double BigramDiceSimilarity(string s1, string s2)
    {
        s1 = s1.Trim().ToLowerInvariant();
        s2 = s2.Trim().ToLowerInvariant();
        if (s1 == s2) return 1.0;
        if (s1.Length < 2 || s2.Length < 2) return 0.0;

        var counts1 = BigramCounts(s1);
        var counts2 = BigramCounts(s2);

        var intersection = 0;
        foreach (var (bigram, count1) in counts1)
        {
            if (counts2.TryGetValue(bigram, out var count2))
                intersection += Math.Min(count1, count2);
        }

        var total1 = s1.Length - 1;
        var total2 = s2.Length - 1;
        return 2.0 * intersection / (total1 + total2);
    }

    private static Dictionary<string, int> BigramCounts(string s)
    {
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < s.Length - 1; i++)
        {
            var bigram = s.Substring(i, 2);
            counts[bigram] = counts.GetValueOrDefault(bigram) + 1;
        }
        return counts;
    }
}
