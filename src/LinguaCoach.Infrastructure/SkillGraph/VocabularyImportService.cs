using System.Text.RegularExpressions;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <inheritdoc cref="IVocabularyImportService"/>
public sealed partial class VocabularyImportService : IVocabularyImportService
{
    /// <summary>Spelling/case variants of the same real CEFR-J topic category, collapsed to one
    /// canonical form. Exact-match only (never a fuzzy merge) — anything not listed here is trusted
    /// as its own distinct category exactly as the CSV wrote it.</summary>
    private static readonly Dictionary<string, string> CategoryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["work and jobs"] = "Work and Jobs",
        ["ways of traveling"] = "Ways of Travelling",
        ["ways of travelling"] = "Ways of Travelling",
        ["art"] = "Arts",
        ["arts"] = "Arts",
    };

    public VocabularyImportPreview ParseCsvFiles(string cefrJVocabularyCsv, string octanoveVocabularyCsv)
    {
        var warnings = new List<string>();
        var rows = new List<VocabularyWordRow>();
        rows.AddRange(ParseCefrJVocabulary(cefrJVocabularyCsv, warnings));
        rows.AddRange(ParseOctanoveVocabulary(octanoveVocabularyCsv, warnings));

        var categorized = rows.Where(r => r.Category is not null).ToList();
        var uncategorized = rows.Where(r => r.Category is null).ToList();

        var containers = categorized
            .GroupBy(r => r.Category!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var leaves = g.Select(BuildLeaf).OrderBy(l => l.Headword, StringComparer.OrdinalIgnoreCase).ToList();
                var easiestLevel = leaves
                    .Select(l => l.CefrLevel)
                    .OrderBy(CefrOrdinal)
                    .First();
                return new VocabularyProposedContainer(
                    Key: $"vocabulary.topic_{Slug(g.Key)}",
                    Title: g.Key,
                    CefrLevel: easiestLevel,
                    Leaves: leaves);
            })
            .ToList();

        var uncategorizedLeaves = uncategorized.Select(BuildLeaf).ToList();

        return new VocabularyImportPreview(containers, uncategorizedLeaves, warnings);
    }

    private static VocabularyProposedLeaf BuildLeaf(VocabularyWordRow row)
    {
        var key = $"vocabulary.cefrj_{Slug(row.Headword)}_{Slug(row.PartOfSpeech)}.{row.CefrLevel.ToLowerInvariant()}";
        var title = $"{Clean(row.Headword)} ({row.PartOfSpeech})";
        return new VocabularyProposedLeaf(key, title, row.CefrLevel, row.Headword, row.PartOfSpeech,
            Description: $"Vocabulary word: {Clean(row.Headword)} ({row.PartOfSpeech}), CEFR {row.CefrLevel}. Real definition pending AI pass.");
    }

    private static readonly string[] CefrLevelsOrdered = ["A1", "A2", "B1", "B2", "C1", "C2"];
    private static int CefrOrdinal(string level)
    {
        var i = Array.IndexOf(CefrLevelsOrdered, level.ToUpperInvariant());
        return i < 0 ? int.MaxValue : i;
    }

    // ── CEFR-J Vocabulary Profile: headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold ──

    private static List<VocabularyWordRow> ParseCefrJVocabulary(string csvContent, List<string> warnings)
    {
        var rows = new List<VocabularyWordRow>();
        foreach (var fields in ParseCsvRows(csvContent))
        {
            if (fields.Count < 4) continue;
            var headword = fields[0].Trim();
            var pos = fields[1].Trim();
            var cefr = fields[2].Trim().ToUpperInvariant();
            if (headword.Length == 0 || pos.Length == 0 || !CefrLevelConstants.IsValid(cefr))
            {
                if (headword.Length > 0) warnings.Add($"CEFR-J vocab row skipped (invalid CEFR level): '{headword}'.");
                continue;
            }
            var category = NormalizeCategory(fields.Count > 3 ? fields[3] : null);
            rows.Add(new VocabularyWordRow(headword, pos, cefr, category));
        }
        return rows;
    }

    // ── Octanove Vocabulary Profile C1/C2: headword,pos,CEFR,notes (no category column) ──────────

    private static List<VocabularyWordRow> ParseOctanoveVocabulary(string csvContent, List<string> warnings)
    {
        var rows = new List<VocabularyWordRow>();
        foreach (var fields in ParseCsvRows(csvContent))
        {
            if (fields.Count < 3) continue;
            var headword = fields[0].Trim();
            var pos = fields[1].Trim();
            var cefr = fields[2].Trim().ToUpperInvariant();
            if (headword.Length == 0 || pos.Length == 0 || !CefrLevelConstants.IsValid(cefr))
            {
                if (headword.Length > 0) warnings.Add($"Octanove vocab row skipped (invalid CEFR level): '{headword}'.");
                continue;
            }
            rows.Add(new VocabularyWordRow(headword, pos, cefr, Category: null));
        }
        return rows;
    }

    private static string? NormalizeCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        return CategoryAliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }

    private static string Clean(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

    private static string Slug(string text)
    {
        var slug = Regex.Replace(text.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(slug) ? "item" : slug;
    }

    // ── CSV parsing — same RFC 4180 quoted-field handling as CefrJGrammarImportService, no
    // external CSV library dependency. ──────────────────────────────────────────────────────────

    private static List<List<string>> ParseCsvRows(string csvContent)
    {
        var lines = csvContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
            .Where(l => l.Length > 0)
            .ToList();
        if (lines.Count == 0) return [];

        return lines.Skip(1).Select(ParseCsvLine).ToList();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}
