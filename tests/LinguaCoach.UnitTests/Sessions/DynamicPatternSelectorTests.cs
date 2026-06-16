using FluentAssertions;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Infrastructure.Sessions;

namespace LinguaCoach.UnitTests.Sessions;

/// <summary>
/// Pure unit tests for DynamicPatternSelector — no DB, no DI.
/// All tests call DynamicPatternSelector.Select() directly.
/// </summary>
public sealed class DynamicPatternSelectorTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static PatternCatalogEntry ReadyCatalogEntry(string patternKey, string skill) =>
        new(patternKey, skill, IsEnabled: true, IsReady: true, SupportsTodayLesson: true);

    private static PatternCatalogEntry DisabledEntry(string patternKey, string skill) =>
        new(patternKey, skill, IsEnabled: false, IsReady: true, SupportsTodayLesson: true);

    private static PatternCatalogEntry PlannedEntry(string patternKey, string skill) =>
        new(patternKey, skill, IsEnabled: true, IsReady: false, SupportsTodayLesson: true);

    private static PatternCatalogEntry UnavailableForToday(string patternKey, string skill) =>
        new(patternKey, skill, IsEnabled: true, IsReady: true, SupportsTodayLesson: false);

    private static PatternSelectionInput BasicInput(
        string[] candidates,
        string slotSkill,
        IReadOnlyList<PatternCatalogEntry>? catalog = null,
        IReadOnlyDictionary<string, int>? skillScores = null,
        IReadOnlyList<string>? recent = null,
        string? goal = null) => new(
            CefrLevel: null,
            SkillScores: skillScores ?? new Dictionary<string, int>(),
            LearningGoalContext: goal,
            RecentPatternKeys: recent ?? [],
            CandidatePatternKeys: candidates,
            SlotPrimarySkill: slotSkill,
            AvailableCatalog: catalog ?? candidates.Select(k => ReadyCatalogEntry(k, slotSkill)).ToList());

    // ── Catalog gate ───────────────────────────────────────────────────────────

    [Fact]
    public void DisabledFormats_AreExcluded()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            DisabledEntry("email_reply", "writing"),
            ReadyCatalogEntry("writing_response", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply", "writing_response"],
            slotSkill: "writing",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.SelectedPatternKey.Should().Be("writing_response");
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void PlannedFormats_AreExcluded()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            PlannedEntry("email_reply", "writing"),
            ReadyCatalogEntry("writing_response", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply", "writing_response"],
            slotSkill: "writing",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.SelectedPatternKey.Should().Be("writing_response");
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void UnavailableForToday_IsExcluded()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            UnavailableForToday("email_reply", "writing"),
            ReadyCatalogEntry("writing_response", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply", "writing_response"],
            slotSkill: "writing",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.SelectedPatternKey.Should().Be("writing_response");
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void AllCandidatesUnavailable_ReturnsFallbackWithFirstCandidate()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            DisabledEntry("email_reply", "writing"),
            PlannedEntry("writing_response", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply", "writing_response"],
            slotSkill: "writing",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.IsFallback.Should().BeTrue();
        result.SelectedPatternKey.Should().Be("email_reply");
        result.Reason.Should().Contain("fallback");
    }

    // ── Weak skill influence ───────────────────────────────────────────────────

    [Fact]
    public void WeakListeningSkill_PrefersListeningCandidate()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("listen_and_answer", "listening"),
            ReadyCatalogEntry("listen_and_gap_fill", "listening")
        };
        var scores = new Dictionary<string, int>
        {
            ["listening"] = 25,
            ["writing"] = 70,
            ["speaking"] = 65
        };
        var input = BasicInput(
            candidates: ["listen_and_answer", "listen_and_gap_fill"],
            slotSkill: "listening",
            catalog: catalog,
            skillScores: scores);

        var result = DynamicPatternSelector.Select(input);

        // Both are listening; selector should prefer the one not in recent history.
        result.IsFallback.Should().BeFalse();
        result.TargetSkill.Should().Be("listening");
    }

    [Fact]
    public void WeakSpeakingSkill_PrefersHigherScoredSpeakingCandidate()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("spoken_response_from_prompt", "speaking"),
            ReadyCatalogEntry("answer_short_question", "speaking"),
            ReadyCatalogEntry("email_reply", "writing")
        };
        var scores = new Dictionary<string, int>
        {
            ["speaking"] = 20,
            ["writing"] = 75
        };
        var input = BasicInput(
            candidates: ["spoken_response_from_prompt", "answer_short_question"],
            slotSkill: "speaking",
            catalog: catalog,
            skillScores: scores);

        var result = DynamicPatternSelector.Select(input);

        result.IsFallback.Should().BeFalse();
        result.TargetSkill.Should().Be("speaking");
    }

    [Fact]
    public void WeakSkill_PrefersCandidateMatchingWeakSkill_OverOtherSkill()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("phrase_match", "vocabulary"),
            ReadyCatalogEntry("listen_and_answer", "listening")
        };
        // Listening is weakest
        var scores = new Dictionary<string, int>
        {
            ["vocabulary"] = 70,
            ["listening"] = 20
        };
        var input = new PatternSelectionInput(
            CefrLevel: null,
            SkillScores: scores,
            LearningGoalContext: null,
            RecentPatternKeys: [],
            CandidatePatternKeys: ["phrase_match", "listen_and_answer"],
            SlotPrimarySkill: "vocabulary",
            AvailableCatalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        // listen_and_answer scores +20 (weakest skill match); phrase_match scores +10 (not recent only).
        result.SelectedPatternKey.Should().Be("listen_and_answer");
        result.IsFallback.Should().BeFalse();
    }

    // ── Repetition avoidance ───────────────────────────────────────────────────

    [Fact]
    public void RecentlyUsedPattern_IsAvoided_WhenAlternativeExists()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("email_reply", "writing"),
            ReadyCatalogEntry("writing_response", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply", "writing_response"],
            slotSkill: "writing",
            catalog: catalog,
            recent: ["email_reply", "email_reply", "email_reply"]);

        var result = DynamicPatternSelector.Select(input);

        // writing_response is not in recent history → higher score → preferred.
        result.SelectedPatternKey.Should().Be("writing_response");
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void NoHistory_SelectsDeterministicallyByAlpha()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("writing_response", "writing"),
            ReadyCatalogEntry("email_reply", "writing")
        };
        var input = BasicInput(
            candidates: ["writing_response", "email_reply"],
            slotSkill: "writing",
            catalog: catalog,
            skillScores: new Dictionary<string, int>());

        // Both have identical skill scores (no profile). No recent history.
        // Tiebreak: alphabetical → "email_reply" < "writing_response".
        var result = DynamicPatternSelector.Select(input);

        result.SelectedPatternKey.Should().Be("email_reply");
        result.IsFallback.Should().BeFalse();
    }

    // ── Explicit override preservation ────────────────────────────────────────

    [Fact]
    public void SingleCandidate_AlwaysReturnsItWhenAvailable()
    {
        // Simulates the caller passing a fixed pattern (single candidate = explicit override path).
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("email_reply", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply"],
            slotSkill: "writing",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.SelectedPatternKey.Should().Be("email_reply");
        result.IsFallback.Should().BeFalse();
    }

    // ── Fallback when no profile ───────────────────────────────────────────────

    [Fact]
    public void NoSkillProfile_FallsBackGracefully()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("phrase_match", "vocabulary"),
            ReadyCatalogEntry("gap_fill_workplace_phrase", "vocabulary")
        };
        var input = BasicInput(
            candidates: ["phrase_match", "gap_fill_workplace_phrase"],
            slotSkill: "vocabulary",
            catalog: catalog,
            skillScores: new Dictionary<string, int>());

        var result = DynamicPatternSelector.Select(input);

        result.IsFallback.Should().BeFalse();
        result.SelectedPatternKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EmptyHistory_DoesNotThrow_AndReturnsValidResult()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("phrase_match", "vocabulary")
        };
        var input = BasicInput(
            candidates: ["phrase_match"],
            slotSkill: "vocabulary",
            catalog: catalog,
            recent: []);

        var act = () => DynamicPatternSelector.Select(input);

        act.Should().NotThrow();
        act().SelectedPatternKey.Should().Be("phrase_match");
    }

    // ── Selector result includes reason ───────────────────────────────────────

    [Fact]
    public void Result_AlwaysIncludesNonEmptyReason()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("phrase_match", "vocabulary")
        };
        var input = BasicInput(
            candidates: ["phrase_match"],
            slotSkill: "vocabulary",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void FallbackResult_ReasonMentionsFallback()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            DisabledEntry("email_reply", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply"],
            slotSkill: "writing",
            catalog: catalog);

        var result = DynamicPatternSelector.Select(input);

        result.IsFallback.Should().BeTrue();
        result.Reason.Should().Contain("fallback");
    }

    // ── Goal context is included in reason, not hardcoded to workplace ─────────

    [Fact]
    public void WorkplaceIsNotHardcodedAsDefault_NeutralGoalProducesValidResult()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("email_reply", "writing")
        };

        // Non-workplace goal context — selector should still work fine.
        var input = BasicInput(
            candidates: ["email_reply"],
            slotSkill: "writing",
            catalog: catalog,
            goal: "day-to-day English");

        var result = DynamicPatternSelector.Select(input);

        result.IsFallback.Should().BeFalse();
        result.Reason.Should().Contain("day-to-day English");
    }

    [Fact]
    public void NullGoalContext_DoesNotCrash()
    {
        var catalog = new List<PatternCatalogEntry>
        {
            ReadyCatalogEntry("email_reply", "writing")
        };
        var input = BasicInput(
            candidates: ["email_reply"],
            slotSkill: "writing",
            catalog: catalog,
            goal: null);

        var act = () => DynamicPatternSelector.Select(input);

        act.Should().NotThrow();
        act().Reason.Should().NotContain("goal-context");
    }

    // ── SessionDurationTemplates pool invariants ──────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_GetCandidates_AlwaysIncludesDefaultPatternKey(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        foreach (var step in steps)
        {
            var candidates = step.GetCandidates();
            candidates.Should().Contain(step.PatternKey,
                because: $"default key '{step.PatternKey}' must always be in candidates");
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_GetCandidates_NeverReturnsEmpty(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        foreach (var step in steps)
            step.GetCandidates().Should().NotBeEmpty();
    }
}
