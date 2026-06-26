using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Curriculum;

namespace LinguaCoach.UnitTests.Curriculum;

/// <summary>
/// Unit tests for CurriculumValidationService — Phase 11B.
/// Uses ValidateSet (pure/sync) to avoid database dependency.
/// </summary>
public sealed class CurriculumValidationServiceTests
{
    private static readonly ICurriculumValidationService Service =
        new CurriculumValidationService(new NullCurriculumSyllabusQuery());

    // ── 1: empty list ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateSet_EmptyList_ReturnsValid()
    {
        var result = Service.ValidateSet([]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.TotalObjectivesChecked);
    }

    // ── 2: duplicate key ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateSet_DuplicateKey_ReturnsError()
    {
        var obj = MakeObjective("a1.speaking.test", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking);
        var result = Service.ValidateSet([obj, obj]);

        Assert.Contains(result.Errors, e => e.Code == CurriculumValidationCodes.DuplicateKey
                                          && e.ObjectiveKey == "a1.speaking.test");
    }

    // ── 3: invalid CEFR level ────────────────────────────────────────────────

    [Fact]
    public void ValidateSet_InvalidCefrLevel_ReturnsError()
    {
        var obj = MakeObjectiveOverride("a1.speaking.test", cefrLevel: "X9", skill: CurriculumSkillConstants.Speaking);
        var result = Service.ValidateSet([obj]);

        Assert.Contains(result.Errors, e => e.Code == CurriculumValidationCodes.InvalidCefr);
    }

    // ── 4: invalid primary skill ─────────────────────────────────────────────

    [Fact]
    public void ValidateSet_InvalidPrimarySkill_ReturnsError()
    {
        var obj = MakeObjectiveOverride("a1.speaking.test", cefrLevel: CefrLevelConstants.A1, skill: "dancing");
        var result = Service.ValidateSet([obj]);

        Assert.Contains(result.Errors, e => e.Code == CurriculumValidationCodes.InvalidSkill);
    }

    // ── 5: missing title ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateSet_MissingTitle_ReturnsError()
    {
        var obj = MakeObjectiveOverride("a1.speaking.test", title: "");
        var result = Service.ValidateSet([obj]);

        Assert.Contains(result.Errors, e => e.Code == CurriculumValidationCodes.MissingTitle);
    }

    // ── 6: prerequisite not found ────────────────────────────────────────────

    [Fact]
    public void ValidateSet_PrereqNotFound_ReturnsError()
    {
        var obj = MakeObjective("a2.speaking.test", CefrLevelConstants.A2, CurriculumSkillConstants.Speaking,
            prereqKeys: ["a1.speaking.missing"]);
        var result = Service.ValidateSet([obj]);

        Assert.Contains(result.Errors, e => e.Code == CurriculumValidationCodes.PrereqNotFound
                                          && e.ObjectiveKey == "a2.speaking.test");
    }

    // ── 7: circular prerequisite chain ───────────────────────────────────────

    [Fact]
    public void ValidateSet_CircularPrereq_ReturnsError()
    {
        // A → B → A: cycle
        var a = MakeObjective("obj.a", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking,
            prereqKeys: ["obj.b"]);
        var b = MakeObjective("obj.b", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking,
            prereqKeys: ["obj.a"]);

        var result = Service.ValidateSet([a, b]);

        Assert.Contains(result.Errors, e => e.Code == CurriculumValidationCodes.PrereqCircular);
    }

    // ── 8: disabled prerequisite ─────────────────────────────────────────────

    [Fact]
    public void ValidateSet_DisabledPrereq_ReturnsWarning()
    {
        var prereq = MakeObjective("a1.speaking.base", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking,
            isActive: false);
        var obj = MakeObjective("a2.speaking.test", CefrLevelConstants.A2, CurriculumSkillConstants.Speaking,
            prereqKeys: ["a1.speaking.base"]);

        var result = Service.ValidateSet([prereq, obj]);

        Assert.Contains(result.Warnings, w => w.Code == CurriculumValidationCodes.PrereqDisabled
                                            && w.ObjectiveKey == "a2.speaking.test");
    }

    // ── 9: invalid context tag ───────────────────────────────────────────────

    [Fact]
    public void ValidateSet_InvalidContextTag_ReturnsWarning()
    {
        var obj = MakeObjective("a1.speaking.test", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking,
            contextTags: ["not_a_real_tag"]);

        var result = Service.ValidateSet([obj]);

        Assert.Contains(result.Warnings, w => w.Code == CurriculumValidationCodes.InvalidContextTag);
    }

    // ── 10: coverage gap ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateSet_CoverageGap_ReturnsGap()
    {
        // Only one active A1/speaking objective — many gaps will be reported.
        var obj = MakeObjective("a1.speaking.test", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking);
        var result = Service.ValidateSet([obj]);

        // At minimum A1/reading should be a gap.
        Assert.Contains(result.CoverageGaps, g =>
            g.CefrLevel == CefrLevelConstants.A1 && g.Skill == CurriculumSkillConstants.Reading);
    }

    // ── 11: non-runnable skill warning ───────────────────────────────────────

    [Fact]
    public void ValidateSet_NonRunnableSkill_ReturnsWarning()
    {
        var obj = MakeObjective("a1.grammar.test", CefrLevelConstants.A1, CurriculumSkillConstants.Grammar);
        var result = Service.ValidateSet([obj]);

        Assert.Contains(result.Warnings, w => w.Code == CurriculumValidationCodes.SkillNotRunnable
                                            && w.ObjectiveKey == "a1.grammar.test");
    }

    // ── 12: valid objectives → IsValid ───────────────────────────────────────

    [Fact]
    public void ValidateSet_ValidObjectives_IsValid()
    {
        var objs = BuildValidCoverageSet();
        var result = Service.ValidateSet(objs);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.CoverageGaps);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a valid objective using the domain constructor.</summary>
    private static CurriculumObjective MakeObjective(
        string key,
        string cefrLevel,
        string skill,
        IReadOnlyList<string>? contextTags = null,
        IReadOnlyList<string>? prereqKeys = null,
        bool isActive = true)
    {
        var tagsJson = contextTags is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(contextTags)
            : $"""["{CurriculumContextTagConstants.GeneralEnglish}"]""";
        var prereqJson = prereqKeys is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(prereqKeys)
            : "[]";

        var obj = new CurriculumObjective(
            key: key,
            title: $"Title for {key}",
            description: $"Description for {key}.",
            cefrLevel: cefrLevel,
            primarySkill: skill,
            contextTagsJson: tagsJson,
            prerequisiteKeysJson: prereqJson,
            isActive: isActive);

        if (!isActive)
            obj.Deactivate();

        return obj;
    }

    /// <summary>
    /// Creates an objective with overridden property values using property setters via reflection.
    /// The domain constructor validates CEFR/skill/title, so we bypass it to test the service logic.
    /// </summary>
    private static CurriculumObjective MakeObjectiveOverride(
        string key,
        string cefrLevel = "A1",
        string skill = "speaking",
        string title = "Test Title",
        string description = "Test description.")
    {
        // Start with a valid object, then override specific properties.
        var validCefrLevel = CefrLevelConstants.IsValid(cefrLevel) ? cefrLevel : CefrLevelConstants.A1;
        var validSkill = CurriculumSkillConstants.All.Contains(skill, StringComparer.OrdinalIgnoreCase)
            ? skill : CurriculumSkillConstants.Speaking;
        var validTitle = string.IsNullOrWhiteSpace(title) ? "Temp" : title;

        var obj = new CurriculumObjective(
            key: key,
            title: validTitle,
            description: description,
            cefrLevel: validCefrLevel,
            primarySkill: validSkill,
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}"]""",
            prerequisiteKeysJson: "[]",
            isActive: true);

        // Override with the test-specific invalid values via reflection on auto-property backing fields.
        OverrideProperty(obj, "CefrLevel", cefrLevel);
        OverrideProperty(obj, "PrimarySkill", skill);
        OverrideProperty(obj, "Title", title);

        return obj;
    }

    private static void OverrideProperty(CurriculumObjective obj, string propName, string value)
    {
        // EF Core backing field convention: <PropName>k__BackingField
        var fieldName = $"<{propName}>k__BackingField";
        var field = typeof(CurriculumObjective)
            .GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }

    /// <summary>Builds a minimal set covering all A1-B2 x core-skill combos.</summary>
    private static IReadOnlyList<CurriculumObjective> BuildValidCoverageSet()
    {
        string[] levels = [CefrLevelConstants.A1, CefrLevelConstants.A2, CefrLevelConstants.B1, CefrLevelConstants.B2];
        string[] skills =
        [
            CurriculumSkillConstants.Speaking,
            CurriculumSkillConstants.Listening,
            CurriculumSkillConstants.Reading,
            CurriculumSkillConstants.Writing,
            CurriculumSkillConstants.Grammar,
            CurriculumSkillConstants.Vocabulary,
            CurriculumSkillConstants.Pronunciation,
        ];

        return levels.SelectMany(level => skills.Select(skill =>
            MakeObjective($"{level.ToLower()}.{skill}.coverage_test", level, skill)))
            .ToList();
    }

    // ── Null stub ─────────────────────────────────────────────────────────────

    private sealed class NullCurriculumSyllabusQuery : ICurriculumSyllabusQuery
    {
        public Task<IReadOnlyList<CurriculumObjective>> GetActiveObjectivesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAsync(string cefrLevel, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndSkillAsync(string cefrLevel, string primarySkill, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndContextAsync(string cefrLevel, string contextTag, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndFocusAreaAsync(string cefrLevel, string focusArea, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetPrerequisitesAsync(string objectiveKey, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetCandidatesForStudentAsync(string? cefrLevel, IReadOnlyList<string> contextTags, IReadOnlyList<string> focusAreas, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<CurriculumObjective?> GetByKeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult<CurriculumObjective?>(null);
    }
}
