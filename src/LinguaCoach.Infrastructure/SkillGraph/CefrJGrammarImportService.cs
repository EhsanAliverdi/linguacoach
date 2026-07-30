using System.Text.RegularExpressions;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <inheritdoc cref="ICefrJGrammarImportService"/>
public sealed partial class CefrJGrammarImportService : ICefrJGrammarImportService
{
    /// <summary>Below this <see cref="TextSimilarity.BigramDiceSimilarity"/> score, a CEFR-J family
    /// is proposed as a brand-new container rather than nested under an existing node — deliberately
    /// lower than the near-duplicate detector's 0.85 (that flags near-IDENTICAL nodes for merging;
    /// this is matching a CEFR-J family's collective topic onto a broader existing node's title,
    /// e.g. "I am" onto "Verb 'to be': am/is/are (statements)" — a real but looser match).</summary>
    public const double ContainerMatchThreshold = 0.35;

    public CefrJGrammarImportPreview ParseAndProposeMapping(
        string csvContent,
        IReadOnlyList<CefrJExistingGrammarNodeCandidate> existingGrammarNodes)
    {
        var rows = ParseCsv(csvContent);
        var warnings = new List<string>();

        var families = new Dictionary<string, List<CefrJRow>>();
        foreach (var row in rows)
        {
            var familyId = row.Id.Contains('-') ? row.Id[..row.Id.IndexOf('-')] : row.Id;
            if (!families.TryGetValue(familyId, out var members))
                families[familyId] = members = [];
            members.Add(row);
        }

        var containers = new List<CefrJProposedContainer>();
        var standaloneLeaves = new List<CefrJProposedLeaf>();

        foreach (var (familyId, members) in families)
        {
            var baseRow = members.FirstOrDefault(m => !m.Id.Contains('-'));
            if (baseRow is null)
            {
                warnings.Add($"Family '{familyId}': no base row found (only hyphenated children) — skipped.");
                continue;
            }

            var (level, confidence, source) = ResolveCefrLevel(baseRow);
            // Only the true last-resort case (zero usable columns anywhere) is a review-screen
            // warning — resolving via a real fallback column (Core Inventory/EGP/GSELO) is still
            // genuine evidence, just not CEFR-J's own. Both still get CefrConfidence.Fallback
            // (persisted, queryable) — this warning list is specifically "needs a human's eyes
            // before applying," matching the original behavior this preview screen always had.
            if (source == "defaulted")
                warnings.Add($"Row {baseRow.Id} ('{baseRow.GrammaticalItem}'): no usable CEFR level column, defaulted to {level} — review before applying.");

            if (members.Count == 1)
            {
                // No AFF/NEG/INT siblings — this row is a standalone grammar point, not a family.
                var leaf = BuildLeaf(baseRow, level, confidence, source);
                standaloneLeaves.Add(leaf);
                continue;
            }

            var containerTitle = $"{Clean(baseRow.GrammaticalItem)} (all forms)";
            var containerKey = $"grammar.cefrj_family_{Slug(baseRow.ShorthandCode)}.{level.ToLowerInvariant()}";
            var band = ResolveDifficultyBand(baseRow);

            var (matchedId, matchedKey, matchConfidence) = FindBestContainerMatch(containerTitle, existingGrammarNodes);

            var leaves = members
                .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .Select(m =>
                {
                    var (leafLevel, leafConfidence, leafSource) = ResolveCefrLevel(m);
                    if (leafSource == "defaulted" && m.Id != baseRow.Id)
                        warnings.Add($"Row {m.Id} ('{m.GrammaticalItem}'): no usable CEFR level column, defaulted to {leafLevel} — review before applying.");
                    return BuildLeaf(m, leafLevel, leafConfidence, leafSource);
                })
                .ToList();

            containers.Add(new CefrJProposedContainer(
                containerKey, containerTitle, level, band,
                matchedId, matchedKey, matchConfidence, leaves, confidence, source));
        }

        return new CefrJGrammarImportPreview(containers, standaloneLeaves, warnings);
    }

    private static CefrJProposedLeaf BuildLeaf(CefrJRow row, string level, CefrConfidence confidence, string source)
    {
        var key = $"grammar.cefrj_{Slug(row.ShorthandCode)}.{level.ToLowerInvariant()}";
        var title = Clean(row.GrammaticalItem);
        var band = ResolveDifficultyBand(row);
        return new CefrJProposedLeaf(key, title, level, band, row.Id, confidence, source);
    }

    private static (Guid? Id, string? Key, double? Confidence) FindBestContainerMatch(
        string containerTitle, IReadOnlyList<CefrJExistingGrammarNodeCandidate> existingGrammarNodes)
    {
        CefrJExistingGrammarNodeCandidate? best = null;
        var bestScore = 0.0;
        foreach (var candidate in existingGrammarNodes)
        {
            var score = TextSimilarity.BigramDiceSimilarity(containerTitle, candidate.Title);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= ContainerMatchThreshold && best is not null
            ? (best.Id, best.Key, bestScore)
            : (null, null, null);
    }

    /// <summary>CEFR-J Level is blank on ~66% of real CSV rows — falls back to Core Inventory, then
    /// the first recognizable CEFR token inside EGP, then GSELO, before defaulting to A1.
    /// Phase GSG-1 (2026-07-31) — the 2026-07-30 seed audit found this fallback chain's outcome was
    /// only ever surfaced as a warning string shown once on the review screen and then discarded;
    /// 145 of 592 existing grammar nodes ended up with an unattested A1 as a result. Now returns a
    /// <see cref="CefrConfidence"/>/source pair that flows all the way into the created
    /// <see cref="Domain.Entities.SkillGraphNode"/>, so "unattested" is a queryable fact, not a lost
    /// console message.</summary>
    private static (string Level, CefrConfidence Confidence, string Source) ResolveCefrLevel(CefrJRow row)
    {
        var fromCefrJ = ExtractLevelToken(row.CefrJLevel);
        if (fromCefrJ is not null) return (fromCefrJ, CefrConfidence.Attested, "cefrj");

        var fromCore = ExtractLevelToken(row.CoreInventory);
        if (fromCore is not null) return (fromCore, CefrConfidence.Fallback, "coreInventory");

        var fromEgp = ExtractLevelToken(row.Egp);
        if (fromEgp is not null) return (fromEgp, CefrConfidence.Fallback, "egp");

        var fromGselo = ExtractLevelToken(row.Gselo);
        if (fromGselo is not null) return (fromGselo, CefrConfidence.Fallback, "gselo");

        return (CefrLevelConstants.A1, CefrConfidence.Fallback, "defaulted");
    }

    private static readonly Regex LevelTokenPattern = LevelTokenRegex();

    private static string? ExtractLevelToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var match = LevelTokenPattern.Match(raw);
        if (!match.Success) return null;
        var token = match.Value.ToUpperInvariant();
        return CefrLevelConstants.IsValid(token) ? token : null;
    }

    /// <summary>The CSV's own sub-level suffix (e.g. "B1.<b>2</b>") is the real, human-curated
    /// difficulty ordering within a coarse CEFR level (CEFR-J's headline finding — <c>does</c>-
    /// negation is a full level harder than <c>do</c>-negation despite both being "present simple").
    /// Absent a sub-level, defaults to band 1 (same default the domain constructor already uses).</summary>
    private static int ResolveDifficultyBand(CefrJRow row)
    {
        var raw = row.CefrJLevel.Trim().TrimEnd('*');
        var dotIndex = raw.IndexOf('.');
        if (dotIndex < 0 || dotIndex + 1 >= raw.Length) return 1;

        var suffix = raw[(dotIndex + 1)..];
        return int.TryParse(suffix, out var band) ? Math.Clamp(band, 1, 5) : 1;
    }

    private static string Clean(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

    private static string Slug(string text)
    {
        var slug = Regex.Replace(text.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(slug) ? "item" : slug;
    }

    [GeneratedRegex("A1|A2|B1|B2|C1|C2", RegexOptions.IgnoreCase)]
    private static partial Regex LevelTokenRegex();

    // ── CSV parsing — standard RFC 4180 quoted-field handling (the real file has quoted fields
    // containing commas, e.g. "A1-A2, C1"). Deterministic, no external CSV library dependency. ────

    private sealed record CefrJRow(
        string Id, string ShorthandCode, string GrammaticalItem, string SentenceType,
        string CefrJLevel, string CoreInventory, string Egp, string Gselo);

    private static List<CefrJRow> ParseCsv(string csvContent)
    {
        var lines = csvContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
            .Where(l => l.Length > 0)
            .ToList();
        if (lines.Count == 0) return [];

        var rows = new List<CefrJRow>();
        // header: ID,Shorthand Code,Grammatical Item,Sentence Type,CEFR-J Level,FREQ*DISP,Core Inventory,EGP,GSELO,Notes
        foreach (var line in lines.Skip(1))
        {
            var fields = ParseCsvLine(line);
            if (fields.Count < 9) continue;
            var id = fields[0].Trim();
            if (id.Length == 0) continue;

            rows.Add(new CefrJRow(
                Id: id,
                ShorthandCode: fields[1].Trim(),
                GrammaticalItem: fields[2].Trim(),
                SentenceType: fields[3].Trim(),
                CefrJLevel: fields[4].Trim(),
                CoreInventory: fields[6].Trim(),
                Egp: fields[7].Trim(),
                Gselo: fields[8].Trim()));
        }

        return rows;
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
