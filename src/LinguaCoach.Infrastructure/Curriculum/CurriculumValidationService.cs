using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.Curriculum;

/// <summary>
/// Deterministic (no AI) validation of curriculum objectives.
/// Checks duplicates, invalid taxonomy, missing fields, prerequisite integrity,
/// circular chains, coverage gaps, and activity-format compatibility.
/// Phase 11B.
/// </summary>
public sealed class CurriculumValidationService : ICurriculumValidationService
{
    private readonly ICurriculumSyllabusQuery _syllabusQuery;

    // Core skills checked for coverage gaps (A1–B2 x these skills).
    private static readonly string[] CoverageSkills =
    [
        CurriculumSkillConstants.Speaking,
        CurriculumSkillConstants.Listening,
        CurriculumSkillConstants.Reading,
        CurriculumSkillConstants.Writing,
        CurriculumSkillConstants.Grammar,
        CurriculumSkillConstants.Vocabulary,
        CurriculumSkillConstants.Pronunciation,
    ];

    private static readonly string[] CoverageLevels =
    [
        CefrLevelConstants.A1,
        CefrLevelConstants.A2,
        CefrLevelConstants.B1,
        CefrLevelConstants.B2,
    ];

    public CurriculumValidationService(ICurriculumSyllabusQuery syllabusQuery)
        => _syllabusQuery = syllabusQuery;

    public async Task<CurriculumValidationResult> ValidateAllActiveAsync(CancellationToken ct = default)
    {
        var objectives = await _syllabusQuery.GetActiveObjectivesAsync(ct);
        return ValidateSet(objectives);
    }

    public CurriculumValidationResult ValidateSet(IReadOnlyList<CurriculumObjective> objectives)
    {
        var errors   = new List<CurriculumValidationIssue>();
        var warnings = new List<CurriculumValidationIssue>();
        var gaps     = new List<CurriculumCoverageGap>();

        // Build lookup structures.
        var keySet   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var byKey    = new Dictionary<string, CurriculumObjective>(StringComparer.OrdinalIgnoreCase);

        // ── Check 1: duplicate keys ───────────────────────────────────────────
        foreach (var obj in objectives)
        {
            keySet.Add(obj.Key);
            if (!seenKeys.Add(obj.Key))
            {
                errors.Add(new(obj.Key, CurriculumValidationCodes.DuplicateKey,
                    $"Objective key '{obj.Key}' appears more than once in the set."));
            }
            else
            {
                byKey[obj.Key] = obj;
            }
        }

        foreach (var obj in objectives)
        {
            // Skip second occurrence of duplicates — already reported.
            if (!byKey.TryGetValue(obj.Key, out var canonical) ||
                !ReferenceEquals(canonical, obj))
                continue;

            // ── Check 2: invalid CEFR level ───────────────────────────────────
            if (!CefrLevelConstants.IsValid(obj.CefrLevel))
                errors.Add(new(obj.Key, CurriculumValidationCodes.InvalidCefr,
                    $"CEFR level '{obj.CefrLevel}' is not a valid CEFR level."));

            // ── Check 3: invalid primary skill ────────────────────────────────
            if (!CurriculumSkillConstants.All.Contains(obj.PrimarySkill, StringComparer.OrdinalIgnoreCase))
                errors.Add(new(obj.Key, CurriculumValidationCodes.InvalidSkill,
                    $"Primary skill '{obj.PrimarySkill}' is not a recognised skill."));

            // ── Check 4: missing title ────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(obj.Title))
                errors.Add(new(obj.Key, CurriculumValidationCodes.MissingTitle,
                    "Objective is missing a title."));

            // ── Check 5: missing description ──────────────────────────────────
            if (string.IsNullOrWhiteSpace(obj.Description))
                errors.Add(new(obj.Key, CurriculumValidationCodes.MissingDescription,
                    "Objective is missing a description."));

            // ── Checks 6–8: prerequisites ─────────────────────────────────────
            var prereqs = ParseJsonArray(obj.PrerequisiteKeysJson);
            foreach (var prereq in prereqs)
            {
                if (!keySet.Contains(prereq))
                {
                    errors.Add(new(obj.Key, CurriculumValidationCodes.PrereqNotFound,
                        $"Prerequisite key '{prereq}' does not exist in the candidate set."));
                }
                else if (byKey.TryGetValue(prereq, out var prereqObj) && !prereqObj.IsActive)
                {
                    // Check 8: prereq is disabled/inactive.
                    warnings.Add(new(obj.Key, CurriculumValidationCodes.PrereqDisabled,
                        $"Prerequisite '{prereq}' is inactive/disabled."));
                }
            }

            // ── Check 9: invalid context tags ─────────────────────────────────
            var contextTags = ParseJsonArray(obj.ContextTagsJson);
            foreach (var tag in contextTags)
            {
                if (!CurriculumContextTagConstants.All.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    warnings.Add(new(obj.Key, CurriculumValidationCodes.InvalidContextTag,
                        $"Context tag '{tag}' is not in the canonical tag set."));
            }

            // ── Check 11: non-runnable skill ──────────────────────────────────
            if (!ActivityCompatibilityConstants.IsRunnable(obj.PrimarySkill)
                && CurriculumSkillConstants.All.Contains(obj.PrimarySkill, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add(new(obj.Key, CurriculumValidationCodes.SkillNotRunnable,
                    $"Primary skill '{obj.PrimarySkill}' has no runnable exercise format yet (planned future format)."));
            }
        }

        // ── Check 7: circular prerequisite chains (DFS) ───────────────────────
        DetectCircularPrerequisites(objectives, byKey, errors);

        // ── Check 10: coverage gaps ───────────────────────────────────────────
        var activeByLevelSkill = objectives
            .Where(o => o.IsActive)
            .GroupBy(o => (o.CefrLevel.ToUpperInvariant(), o.PrimarySkill.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var level in CoverageLevels)
        {
            foreach (var skill in CoverageSkills)
            {
                var key = (level.ToUpperInvariant(), skill.ToLowerInvariant());
                if (!activeByLevelSkill.TryGetValue(key, out var count) || count == 0)
                {
                    gaps.Add(new CurriculumCoverageGap(level, skill,
                        $"No active objectives found for CEFR {level} / skill '{skill}'."));
                }
            }
        }

        return new CurriculumValidationResult
        {
            Errors = errors,
            Warnings = warnings,
            CoverageGaps = gaps,
            TotalObjectivesChecked = objectives.Count,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void DetectCircularPrerequisites(
        IReadOnlyList<CurriculumObjective> objectives,
        Dictionary<string, CurriculumObjective> byKey,
        List<CurriculumValidationIssue> errors)
    {
        // Build adjacency: key → list of prereq keys that exist in the set.
        // Only include canonical entries (first occurrence; duplicates already reported as errors).
        var adj = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in objectives)
        {
            if (!byKey.TryGetValue(o.Key, out var canonical) || !ReferenceEquals(canonical, o))
                continue; // skip duplicate occurrences
            adj[o.Key] = ParseJsonArray(o.PrerequisiteKeysJson)
                .Where(p => byKey.ContainsKey(p))
                .ToList();
        }

        // Track visit state per node: 0=unvisited, 1=in-stack, 2=done.
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in adj.Keys)
            DfsVisit(key, adj, state, reported, errors);
    }

    private static void DfsVisit(
        string node,
        Dictionary<string, List<string>> adj,
        Dictionary<string, int> state,
        HashSet<string> reported,
        List<CurriculumValidationIssue> errors)
    {
        if (!state.TryGetValue(node, out var s)) s = 0;
        if (s == 2) return; // already fully processed
        if (s == 1)         // back edge: cycle detected
        {
            if (reported.Add(node))
                errors.Add(new(node, CurriculumValidationCodes.PrereqCircular,
                    $"Circular prerequisite chain detected involving objective '{node}'."));
            return;
        }

        state[node] = 1;
        if (adj.TryGetValue(node, out var prereqs))
        {
            foreach (var prereq in prereqs)
                DfsVisit(prereq, adj, state, reported, errors);
        }
        state[node] = 2;
    }

    private static List<string> ParseJsonArray(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
                return [];

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
