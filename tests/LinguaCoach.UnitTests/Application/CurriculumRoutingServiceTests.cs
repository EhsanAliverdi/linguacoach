using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Curriculum;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Application;

/// <summary>
/// Unit tests for CurriculumRoutingService — Phase 10L.
/// Uses a stub ICurriculumSyllabusQuery with in-memory objectives.
/// </summary>
public sealed class CurriculumRoutingServiceTests
{
    // ── CEFR normalization ───────────────────────────────────────────────────

    [Theory]
    [InlineData("B2", "B2")]
    [InlineData("b2", "B2")]
    [InlineData("A1", "A1")]
    [InlineData("C2", "C2")]
    public void NormalizeCefrLevel_CoreLevels_ReturnSelf(string input, string expected)
    {
        var svc = BuildService([]);
        Assert.Equal(expected, svc.NormalizeCefrLevel(input));
    }

    [Theory]
    [InlineData("B2+", "B2")]
    [InlineData("B2-", "B2")]
    [InlineData("C1+", "C1")]
    [InlineData("A2+", "A2")]
    public void NormalizeCefrLevel_PlusMinusSuffix_StripsToCore(string input, string expected)
    {
        var svc = BuildService([]);
        Assert.Equal(expected, svc.NormalizeCefrLevel(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    [InlineData("X9")]
    public void NormalizeCefrLevel_InvalidOrNull_FallsBackToA1(string? input)
    {
        var svc = BuildService([]);
        Assert.Equal(CefrLevelConstants.A1, svc.NormalizeCefrLevel(input));
    }

    // ── Exact-level objective preferred ─────────────────────────────────────

    [Fact]
    public async Task Recommend_ExactLevelMatch_ReturnsNormalReason()
    {
        var objective = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([objective]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Normal, rec.RoutingReason);
        Assert.Equal(CefrLevelConstants.B2, rec.TargetCefrLevel);
        Assert.False(rec.IsLowerLevelContent);
        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Phase D6 — matched objective's subskill surfaces on the recommendation ──

    [Fact]
    public async Task Recommend_ExactLevelMatch_SurfacesObjectiveSubskill()
    {
        var objective = MakeObjective("b2_reading_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Reading, [CurriculumContextTagConstants.GeneralEnglish],
            subskill: "reading.inference");

        var svc = BuildService([objective]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b2_reading_general", rec.CurriculumObjectiveKey);
        Assert.Equal("reading.inference", rec.Subskill);
    }

    [Fact]
    public async Task Recommend_Fallback_HasNullSubskill()
    {
        var svc = BuildService([]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Null(rec.CurriculumObjectiveKey);
        Assert.Null(rec.Subskill);
    }

    // ── Lower-level not selected silently ───────────────────────────────────

    [Fact]
    public async Task Recommend_NoMatchAndReviewNotAllowed_ReturnsFallback_NotLowerLevel()
    {
        // Only a B1 objective exists for a B2 student; review not allowed.
        var objective = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([objective]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false, allowReviewOrScaffold: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Fallback, rec.RoutingReason);
        Assert.Equal(CefrLevelConstants.B2, rec.TargetCefrLevel);
        Assert.False(rec.IsLowerLevelContent);
        Assert.Null(rec.CurriculumObjectiveKey);
    }

    // ── Lower-level selected only with review/scaffold reason ────────────────

    [Fact]
    public async Task Recommend_NoMatchAllowReview_SelectsLowerLevelWithReviewReason()
    {
        var objective = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([objective]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false, allowReviewOrScaffold: true);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Review, rec.RoutingReason);
        Assert.True(rec.IsLowerLevelContent);
        Assert.Equal(CefrLevelConstants.B1, rec.TargetCefrLevel);
        Assert.Equal("b1_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Non-workplace context never routes to workplace ──────────────────────

    [Fact]
    public async Task Recommend_DayToDayEnglish_DoesNotRouteToWorkplace()
    {
        var workplaceObj = MakeObjective("b2_writing_workplace", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.Workplace]);
        var generalObj = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([workplaceObj, generalObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual("b2_writing_workplace", rec.CurriculumObjectiveKey);
        Assert.DoesNotContain(CurriculumContextTagConstants.Workplace, rec.ContextTags);
    }

    [Fact]
    public async Task Recommend_WorkplaceGoal_RoutesToWorkplace()
    {
        var workplaceObj = MakeObjective("b2_writing_workplace", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.Workplace]);

        var svc = BuildService([workplaceObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: true);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b2_writing_workplace", rec.CurriculumObjectiveKey);
    }

    // ── No active objective → safe general_english fallback, not workplace ───

    [Fact]
    public async Task Recommend_NoObjective_FallbackContextIsNotWorkplace()
    {
        var svc = BuildService([]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Fallback, rec.RoutingReason);
        Assert.DoesNotContain(CurriculumContextTagConstants.Workplace, rec.ContextTags);
        Assert.Equal(CefrLevelConstants.B2, rec.TargetCefrLevel);
    }

    // ── Skill filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Recommend_SkillFilter_PrefersMatchingSkill()
    {
        var writing = MakeObjective("b2_writing", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var listening = MakeObjective("b2_listening", CefrLevelConstants.B2,
            CurriculumSkillConstants.Listening, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([writing, listening]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false,
            primarySkill: CurriculumSkillConstants.Listening);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b2_listening", rec.CurriculumObjectiveKey);
        Assert.Equal(CurriculumSkillConstants.Listening, rec.PrimarySkill);
    }

    // ── DifficultyPreference influences DifficultyBand ───────────────────────

    [Fact]
    public async Task Recommend_GentleDifficulty_PrefersLowerBand()
    {
        var easyObj = MakeObjective("b2_writing_easy", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish], difficultyBand: 1);
        var hardObj = MakeObjective("b2_writing_hard", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish], difficultyBand: 5);

        var svc = BuildService([easyObj, hardObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false, difficultyPreference: "Gentle");

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b2_writing_easy", rec.CurriculumObjectiveKey);
    }

    [Fact]
    public async Task Recommend_ChallengingDifficulty_PrefersHigherBand()
    {
        var easyObj = MakeObjective("b2_writing_easy", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish], difficultyBand: 1);
        var hardObj = MakeObjective("b2_writing_hard", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish], difficultyBand: 5);

        var svc = BuildService([easyObj, hardObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false, difficultyPreference: "Challenging");

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b2_writing_hard", rec.CurriculumObjectiveKey);
    }

    // ── Travel / social context routing ──────────────────────────────────────

    [Fact]
    public async Task Recommend_TravelGoal_MapToTravelContextTag()
    {
        var travelObj = MakeObjective("b2_travel", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.Travel]);
        var generalObj = MakeObjective("b2_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([travelObj, generalObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false,
            primaryGoalKey: "travel_english");

        var rec = await svc.RecommendAsync(req);

        Assert.Contains(CurriculumContextTagConstants.Travel, rec.ContextTags);
    }

    // ── Fallback when no active objectives ───────────────────────────────────

    [Fact]
    public async Task Recommend_NullCefrLevel_NormalizesToA1_StillReturnsRecommendation()
    {
        var svc = BuildService([]);
        var req = MakeRequest(null, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(CefrLevelConstants.A1, rec.TargetCefrLevel);
        Assert.NotNull(rec);
    }

    // ── RoutingContextSummary ─────────────────────────────────────────────────

    [Fact]
    public async Task Recommend_WithObjective_RoutingContextSummaryIsNotNull()
    {
        var obj = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([obj]);
        var req = MakeRequest(CefrLevelConstants.B1, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.NotNull(rec.RoutingContextSummary);
        Assert.Contains("Curriculum objective:", rec.RoutingContextSummary);
    }

    [Fact]
    public async Task Recommend_Fallback_RoutingContextSummaryIsNull()
    {
        var svc = BuildService([]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Null(rec.RoutingContextSummary);
    }

    // ── Phase 11B: mastered objective filtering ──────────────────────────────
    // Adaptive Curriculum Sprint 5 — the mastery-exclusion filter itself was removed (Sprint 4 made
    // it permanently dead code: MasteredObjectiveKeys is sourced from skill-graph node keys, an
    // unrelated key space to CurriculumObjective.Key, so it could never match a real candidate).
    // These tests now document that MasteredObjectiveKeys has no effect on candidate selection.

    [Fact]
    public async Task Recommend_MasteredObjectiveKeys_HaveNoEffect_KeySpacesUnrelated()
    {
        var writing = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var listening = MakeObjective("b2_listening_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Listening, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([writing, listening]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false,
                ContextSummary = "test",
                Source = "Structured"
            },
            MasteredObjectiveKeys = ["b2_writing_general"],
            AllowReviewOfMastered = false
        };

        var rec = await svc.RecommendAsync(req);

        // "b2_writing_general" is still a valid candidate even though it's listed as mastered —
        // the mastery-exclusion filter was removed. Recommendation is decided by ordinary scoring,
        // not by mastery status.
        Assert.Contains(rec.CurriculumObjectiveKey, new[] { "b2_writing_general", "b2_listening_general" });
    }

    [Fact]
    public async Task Recommend_MasteredReviewableObjective_IncludedWhenAllowReviewOfMastered()
    {
        // isReviewable=true via the domain constructor default (false), so pass explicitly.
        var masteredReviewable = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish],
            isReviewable: true);

        var svc = BuildService([masteredReviewable]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false,
                ContextSummary = "test",
                Source = "Structured"
            },
            MasteredObjectiveKeys = ["b2_writing_general"],
            AllowReviewOfMastered = true
        };

        var rec = await svc.RecommendAsync(req);

        // Reviewable mastered objective should be included when AllowReviewOfMastered=true.
        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Phase 11C: RoutingMode on request ───────────────────────────────────

    [Fact]
    public async Task Recommend_NewLearningMode_MasteredObjectiveKeys_NoLongerExcluded()
    {
        var mastered = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var other = MakeObjective("b2_listening_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Listening, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([mastered, other]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            Mode = RoutingMode.NewLearning,
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false, ContextSummary = "test", Source = "Structured"
            },
            MasteredObjectiveKeys = ["b2_writing_general"],
            AllowReviewOfMastered = false
        };

        var rec = await svc.RecommendAsync(req);

        Assert.Contains(rec.CurriculumObjectiveKey, new[] { "b2_writing_general", "b2_listening_general" });
    }

    [Fact]
    public async Task Recommend_ReviewMode_IncludesMasteredReviewableObjective()
    {
        var masteredReviewable = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish],
            isReviewable: true);

        var svc = BuildService([masteredReviewable]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            Mode = RoutingMode.Review,
            AllowReviewOfMastered = true,
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false, ContextSummary = "test", Source = "Structured"
            },
            MasteredObjectiveKeys = ["b2_writing_general"]
        };

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Phase 11C: non-runnable skill filter ─────────────────────────────────

    [Fact]
    public async Task Recommend_GrammarOnlyObjective_FilteredOut_FallsBackToGeneral()
    {
        var grammarObj = MakeObjective("b2_grammar_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([grammarObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        // Grammar has no runnable format — routing should fall back.
        Assert.Equal(RoutingReason.Fallback, rec.RoutingReason);
        Assert.Null(rec.CurriculumObjectiveKey);
    }

    [Fact]
    public async Task Recommend_PronunciationOnlyObjective_FilteredOut_FallsBackToGeneral()
    {
        var pronObj = MakeObjective("b2_pronunciation_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Pronunciation, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([pronObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Fallback, rec.RoutingReason);
        Assert.Null(rec.CurriculumObjectiveKey);
    }

    [Fact]
    public async Task Recommend_MixedSkills_RunnableSelectedOverNonRunnable()
    {
        var grammarObj = MakeObjective("b2_grammar_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish], difficultyBand: 1);
        var writingObj = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish], difficultyBand: 2);

        var svc = BuildService([grammarObj, writingObj]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        // Writing is runnable; grammar is not — writing must be selected.
        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
        Assert.Equal(RoutingReason.Normal, rec.RoutingReason);
    }

    // ── Phase 11C-FINAL: non-runnable filter applies to lower-level review path ─

    [Fact]
    public async Task Recommend_LowerLevelReviewPath_NonRunnableFilteredOut()
    {
        // B2 student, no B2 candidates, but B1 grammar (non-runnable) exists.
        // AllowReviewOrScaffold=true — would normally select B1 content.
        // Grammar must still be filtered → fallback expected.
        var grammarB1 = MakeObjective("b1_grammar_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([grammarB1]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false, allowReviewOrScaffold: true);

        var rec = await svc.RecommendAsync(req);

        // Grammar is non-runnable even at lower level — must fall back.
        Assert.Equal(RoutingReason.Fallback, rec.RoutingReason);
        Assert.Null(rec.CurriculumObjectiveKey);
    }

    [Fact]
    public async Task Recommend_LowerLevelReviewPath_RunnableSelectedOverNonRunnable()
    {
        // B2 student, no B2 candidates.
        // B1 has both grammar (non-runnable) and writing (runnable).
        // Writing must be selected.
        var grammarB1 = MakeObjective("b1_grammar_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish]);
        var writingB1 = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([grammarB1, writingB1]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false, allowReviewOrScaffold: true);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Review, rec.RoutingReason);
        Assert.Equal("b1_writing_general", rec.CurriculumObjectiveKey);
        Assert.True(rec.IsLowerLevelContent);
    }

    [Fact]
    public async Task Recommend_MasteredFilterDoesNotReintroduceNonRunnable()
    {
        // Both writing (mastered) and grammar (non-runnable) exist at B2.
        // After non-runnable filter: only writing remains.
        // After mastered filter: writing is excluded, list empties → fallback to original filtered set.
        // Original filtered set has only writing (mastered). Mastered fallback keeps it.
        // Key: grammar must never be selected even as mastered-fallback.
        var writing = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var grammar = MakeObjective("b2_grammar_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([writing, grammar]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            Mode = RoutingMode.NewLearning,
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false, ContextSummary = "test", Source = "Structured"
            },
            // Writing is mastered; grammar is non-runnable.
            // Non-runnable filter runs first, so grammar is already gone.
            // Mastered filter then removes writing → fallback keeps writing (mastered).
            // Grammar must not be selected.
            MasteredObjectiveKeys = ["b2_writing_general"],
            AllowReviewOfMastered = false
        };

        var rec = await svc.RecommendAsync(req);

        // Grammar is non-runnable and must never appear, even as fallback.
        if (rec.CurriculumObjectiveKey is not null)
            Assert.NotEqual("b2_grammar_general", rec.CurriculumObjectiveKey);
    }

    // ── Phase 11C-FINAL: general learner still gets general objectives ────────────

    [Fact]
    public async Task Recommend_GeneralLearner_GetsGeneralObjective()
    {
        var generalObj = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([generalObj]);
        var req = MakeRequest(CefrLevelConstants.B1, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal("b1_writing_general", rec.CurriculumObjectiveKey);
        Assert.DoesNotContain(CurriculumContextTagConstants.Workplace, rec.ContextTags);
    }

    [Fact]
    public async Task Recommend_WorkplaceObjective_OnlyWhenWorkplaceContextSelected()
    {
        var workplaceObj = MakeObjective("b2_writing_workplace", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.Workplace]);
        var generalObj = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([workplaceObj, generalObj]);
        var nonWorkplaceReq = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(nonWorkplaceReq);

        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
        Assert.DoesNotContain(CurriculumContextTagConstants.Workplace, rec.ContextTags);
    }

    [Fact]
    public async Task Recommend_InactiveObjective_NotReturned()
    {
        // Stub only returns active objectives — inactive filtered at query level.
        // The routing service receives an empty candidates list and falls back.
        var svc = BuildService([]);
        var req = MakeRequest(CefrLevelConstants.B2, workplaceSpecific: false);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Fallback, rec.RoutingReason);
        Assert.Null(rec.CurriculumObjectiveKey);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CurriculumRoutingService BuildService(IReadOnlyList<CurriculumObjective> objectives)
        => new(new StubCurriculumSyllabusQuery(objectives), NullLogger<CurriculumRoutingService>.Instance);

    private static CurriculumRoutingRequest MakeRequest(
        string? cefrLevel,
        bool workplaceSpecific,
        bool allowReviewOrScaffold = false,
        string? primarySkill = null,
        string? difficultyPreference = null,
        string? primaryGoalKey = null)
    {
        return new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = cefrLevel,
            PrimarySkill = primarySkill,
            Source = "test",
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = workplaceSpecific,
                PrimaryGoalKey = primaryGoalKey,
                ContextSummary = "test context",
                Source = "Structured"
            },
            DifficultyPreference = difficultyPreference,
            AllowReviewOrScaffold = allowReviewOrScaffold
        };
    }

    private static CurriculumObjective MakeObjective(
        string key,
        string cefrLevel,
        string skill,
        IReadOnlyList<string> contextTags,
        int difficultyBand = 2,
        int recommendedOrder = 0,
        bool isReviewable = false,
        string? subskill = null)
    {
        var contextTagsJson = System.Text.Json.JsonSerializer.Serialize(contextTags);
        var obj = new CurriculumObjective(
            key: key,
            title: $"Test objective: {key}",
            description: "Test description.",
            cefrLevel: cefrLevel,
            primarySkill: skill,
            contextTagsJson: contextTagsJson,
            difficultyBand: difficultyBand,
            recommendedOrder: recommendedOrder,
            isActive: true,
            isReviewable: isReviewable,
            subskill: subskill);
        return obj;
    }

    /// <summary>
    /// Stub implementation of ICurriculumSyllabusQuery backed by an in-memory list.
    /// Returns objectives filtered by CEFR level and context tag overlap.
    /// </summary>
    private sealed class StubCurriculumSyllabusQuery : ICurriculumSyllabusQuery
    {
        private readonly IReadOnlyList<CurriculumObjective> _objectives;

        public StubCurriculumSyllabusQuery(IReadOnlyList<CurriculumObjective> objectives)
            => _objectives = objectives;

        public Task<IReadOnlyList<CurriculumObjective>> GetActiveObjectivesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive).OrderBy(o => o.RecommendedOrder).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAsync(string cefrLevel, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive && o.CefrLevel == cefrLevel.ToUpperInvariant()).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndSkillAsync(string cefrLevel, string primarySkill, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive
                    && o.CefrLevel == cefrLevel.ToUpperInvariant()
                    && o.PrimarySkill == primarySkill.ToLowerInvariant()).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndContextAsync(string cefrLevel, string contextTag, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive
                    && o.CefrLevel == cefrLevel.ToUpperInvariant()
                    && o.ContextTagsJson.Contains(contextTag)).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndFocusAreaAsync(string cefrLevel, string focusArea, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive
                    && o.CefrLevel == cefrLevel.ToUpperInvariant()
                    && o.FocusTagsJson.Contains(focusArea)).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetPrerequisitesAsync(string objectiveKey, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(new List<CurriculumObjective>());

        public Task<IReadOnlyList<CurriculumObjective>> GetCandidatesForStudentAsync(
            string? cefrLevel,
            IReadOnlyList<string> contextTags,
            IReadOnlyList<string> focusAreas,
            CancellationToken ct = default)
        {
            var results = _objectives
                .Where(o => o.IsActive
                    && (cefrLevel is null || o.CefrLevel == cefrLevel.ToUpperInvariant())
                    && contextTags.Any(tag => o.ContextTagsJson.Contains($"\"{tag}\"")))
                .OrderBy(o => o.RecommendedOrder)
                .ToList();

            return Task.FromResult<IReadOnlyList<CurriculumObjective>>(results);
        }

        public Task<CurriculumObjective?> GetByKeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_objectives.FirstOrDefault(o => o.Key == key));
    }
}

// ── Phase 12E — PreferredObjectiveKey routing tests ──────────────────────────

public sealed class CurriculumRoutingServicePreferredObjectiveTests
{
    // ── Test 1: preferred key is selected when valid ─────────────────────────

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_Valid_ReturnsLearningPlanReason()
    {
        var preferred = MakeObjective("b2_writing_workplace", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.Workplace]);
        var other = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred, other]);
        var req = MakeRequest(CefrLevelConstants.B2, preferredObjectiveKey: "b2_writing_workplace");

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.Equal("b2_writing_workplace", rec.CurriculumObjectiveKey);
    }

    // ── Test 2: preferred key rejected when not found in syllabus (inactive/missing) ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_NotInSyllabus_FallsBack()
    {
        var other = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([other]);
        var req = MakeRequest(CefrLevelConstants.B2, preferredObjectiveKey: "b2_unknown_key");

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.NotEqual("b2_unknown_key", rec.CurriculumObjectiveKey);
    }

    // ── Test 3: preferred key rejected on CEFR mismatch without AllowReviewOrScaffold ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_CefrMismatch_NoScaffold_FallsBack()
    {
        // Preferred key is B1 but student is B2; scaffold not allowed.
        var preferred = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var other = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred, other]);
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b1_writing_general",
            allowReviewOrScaffold: false);

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.NotEqual("b1_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 4: preferred key accepted as review/scaffold when AllowReviewOrScaffold=true ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_OneLevelLower_WithScaffold_Accepted()
    {
        // Student B2, preferred key is B1 (exactly one level down).
        var preferred = MakeObjective("b1_writing_general", CefrLevelConstants.B1,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred]);
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b1_writing_general",
            allowReviewOrScaffold: true);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.Equal("b1_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 5: preferred key rejected when skill doesn't match requested skill ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_SkillMismatch_FallsBack()
    {
        var preferred = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var other = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred, other]);
        // Prefer a writing objective but caller requests speaking.
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b2_writing_general",
            primarySkill: CurriculumSkillConstants.Speaking);

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.NotEqual("b2_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 6: preferred key rejected when skill is not runnable ───────────

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_NonRunnableSkill_FallsBack()
    {
        // Grammar is in PlannedSkills, not RunnableSkills.
        var preferred = MakeObjective("b2_grammar_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish]);
        var other = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred, other]);
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b2_grammar_general");

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.NotEqual("b2_grammar_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 7: rejection falls back to existing routing ────────────────────

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_Rejected_ExistingRoutingContinues()
    {
        // Preferred key points to non-runnable grammar objective.
        // Fallback candidate exists at B2.
        var preferred = MakeObjective("b2_grammar_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Grammar, [CurriculumContextTagConstants.GeneralEnglish]);
        var fallback = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred, fallback]);
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b2_grammar_general");

        var rec = await svc.RecommendAsync(req);

        // Normal routing should have picked the speaking objective.
        Assert.Equal(RoutingReason.Normal, rec.RoutingReason);
        Assert.Equal("b2_speaking_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 8: mastered objective excluded when Mode=NewLearning ────────────

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_MasteredObjectiveKeys_NoLongerRejected()
    {
        // Adaptive Curriculum Sprint 5 — the preferred-objective mastery-exclusion rule (former
        // "Rule 5") was removed along with FilterByMastered; see the Phase 11B tests above for why.
        var preferred = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish],
            isReviewable: false);
        var other = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred, other]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            PreferredObjectiveKey = "b2_writing_general",
            ResolvedLearningGoalContext = MakeGoalContext(workplaceSpecific: false),
            MasteredObjectiveKeys = ["b2_writing_general"],
            Mode = RoutingMode.NewLearning,
            AllowReviewOfMastered = false
        };

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 9: mastered reviewable accepted when AllowReviewOfMastered=true ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_MasteredReviewable_AllowReview_Accepted()
    {
        var preferred = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish],
            isReviewable: true);

        var svc = BuildService([preferred]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            PreferredObjectiveKey = "b2_writing_general",
            ResolvedLearningGoalContext = MakeGoalContext(workplaceSpecific: false),
            MasteredObjectiveKeys = ["b2_writing_general"],
            Mode = RoutingMode.Review,
            AllowReviewOfMastered = true
        };

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.Equal("b2_writing_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 10: no preferred key — existing non-workplace default unchanged ─

    [Fact]
    public async Task Recommend_NoPreferredKey_NonWorkplace_DefaultBehaviorUnchanged()
    {
        var obj = MakeObjective("b2_speaking_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([obj]);
        var req = MakeRequest(CefrLevelConstants.B2, preferredObjectiveKey: null);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.Normal, rec.RoutingReason);
        Assert.Equal("b2_speaking_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 11: no silent CEFR downgrade — preferred lower key rejected without scaffold ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_LowerLevel_NoSilentDowngrade()
    {
        // Student B2, preferred key is A2 (two levels down) — must always reject.
        var lower = MakeObjective("a2_writing_general", CefrLevelConstants.A2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);
        var candidate = MakeObjective("b2_writing_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Writing, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([lower, candidate]);
        // AllowReviewOrScaffold = true but A2 is two levels below B2 — still rejected.
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "a2_writing_general",
            allowReviewOrScaffold: true);

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual("a2_writing_general", rec.CurriculumObjectiveKey);
        // Must not silently use A2; should fall back to B2 candidate.
        Assert.Equal(CefrLevelConstants.B2, rec.TargetCefrLevel);
    }

    // ── Test 12: mastery exclusion wins over preferred key (non-reviewable) ──

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_MasteredAndNotReviewable_NoLongerRejected()
    {
        // Adaptive Curriculum Sprint 5 — see the note on
        // Recommend_PreferredObjectiveKey_MasteredObjectiveKeys_NoLongerRejected above.
        var preferred = MakeObjective("b2_vocab_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Vocabulary, [CurriculumContextTagConstants.GeneralEnglish],
            isReviewable: false);

        var svc = BuildService([preferred]);
        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "test",
            PreferredObjectiveKey = "b2_vocab_general",
            ResolvedLearningGoalContext = MakeGoalContext(workplaceSpecific: false),
            MasteredObjectiveKeys = ["b2_vocab_general"],
            Mode = RoutingMode.NewLearning,
            AllowReviewOfMastered = true
        };

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
    }

    // ── Test 13: preferred key with no PrimarySkill filter — any skill accepted ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_NoSkillFilter_AnySkillAccepted()
    {
        var preferred = MakeObjective("b2_listening_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Listening, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([preferred]);
        // No PrimarySkill requested — preferred should be accepted regardless of skill.
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b2_listening_general",
            primarySkill: null);

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.Equal("b2_listening_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 14: preferred key two levels below — rejected even with AllowReviewOrScaffold ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_TwoLevelsBelow_AlwaysRejected()
    {
        // C1 student, A2 preferred key = 3 levels below → must never accept.
        var lower = MakeObjective("a2_speaking_general", CefrLevelConstants.A2,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);
        var candidate = MakeObjective("c1_speaking_general", CefrLevelConstants.C1,
            CurriculumSkillConstants.Speaking, [CurriculumContextTagConstants.GeneralEnglish]);

        var svc = BuildService([lower, candidate]);
        var req = MakeRequest(CefrLevelConstants.C1,
            preferredObjectiveKey: "a2_speaking_general",
            allowReviewOrScaffold: true);

        var rec = await svc.RecommendAsync(req);

        Assert.NotEqual("a2_speaking_general", rec.CurriculumObjectiveKey);
    }

    // ── Test 15: preferred key accepted overrides normal candidate selection ─

    [Fact]
    public async Task Recommend_PreferredObjectiveKey_OverridesScoreBased_Selection()
    {
        // Two B2 general objectives. Normal routing would pick "b2_vocab_general"
        // (lower band = closer to preferred band 2). Preferred key forces "b2_reading_general".
        var preferred = MakeObjective("b2_reading_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Reading, [CurriculumContextTagConstants.GeneralEnglish],
            difficultyBand: 5);  // high band — score-based routing would deprioritise it
        var normal = MakeObjective("b2_vocab_general", CefrLevelConstants.B2,
            CurriculumSkillConstants.Vocabulary, [CurriculumContextTagConstants.GeneralEnglish],
            difficultyBand: 2);

        var svc = BuildService([preferred, normal]);
        var req = MakeRequest(CefrLevelConstants.B2,
            preferredObjectiveKey: "b2_reading_general");

        var rec = await svc.RecommendAsync(req);

        Assert.Equal(RoutingReason.LearningPlan, rec.RoutingReason);
        Assert.Equal("b2_reading_general", rec.CurriculumObjectiveKey);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CurriculumRoutingService BuildService(IReadOnlyList<CurriculumObjective> objectives)
        => new(new StubCurriculumSyllabusQuery(objectives), NullLogger<CurriculumRoutingService>.Instance);

    private static CurriculumRoutingRequest MakeRequest(
        string? cefrLevel,
        string? preferredObjectiveKey = null,
        bool allowReviewOrScaffold = false,
        string? primarySkill = null)
    {
        return new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = cefrLevel,
            PrimarySkill = primarySkill,
            Source = "test",
            PreferredObjectiveKey = preferredObjectiveKey,
            ResolvedLearningGoalContext = MakeGoalContext(workplaceSpecific: false),
            AllowReviewOrScaffold = allowReviewOrScaffold
        };
    }

    private static ResolvedLearningGoalContext MakeGoalContext(bool workplaceSpecific) =>
        new()
        {
            WorkplaceSpecific = workplaceSpecific,
            ContextSummary = "test context",
            Source = "Structured"
        };

    private static CurriculumObjective MakeObjective(
        string key,
        string cefrLevel,
        string skill,
        IReadOnlyList<string> contextTags,
        int difficultyBand = 2,
        bool isReviewable = false)
    {
        var contextTagsJson = System.Text.Json.JsonSerializer.Serialize(contextTags);
        return new CurriculumObjective(
            key: key,
            title: $"Test: {key}",
            description: "Test.",
            cefrLevel: cefrLevel,
            primarySkill: skill,
            contextTagsJson: contextTagsJson,
            difficultyBand: difficultyBand,
            recommendedOrder: 0,
            isActive: true,
            isReviewable: isReviewable);
    }

    private sealed class StubCurriculumSyllabusQuery : ICurriculumSyllabusQuery
    {
        private readonly IReadOnlyList<CurriculumObjective> _objectives;

        public StubCurriculumSyllabusQuery(IReadOnlyList<CurriculumObjective> objectives)
            => _objectives = objectives;

        public Task<IReadOnlyList<CurriculumObjective>> GetActiveObjectivesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAsync(string cefrLevel, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>(
                _objectives.Where(o => o.IsActive && o.CefrLevel == cefrLevel.ToUpperInvariant()).ToList());

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndSkillAsync(string cefrLevel, string primarySkill, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndContextAsync(string cefrLevel, string contextTag, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndFocusAreaAsync(string cefrLevel, string focusArea, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetPrerequisitesAsync(string objectiveKey, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CurriculumObjective>>([]);

        public Task<IReadOnlyList<CurriculumObjective>> GetCandidatesForStudentAsync(
            string? cefrLevel,
            IReadOnlyList<string> contextTags,
            IReadOnlyList<string> focusAreas,
            CancellationToken ct = default)
        {
            var results = _objectives
                .Where(o => o.IsActive
                    && (cefrLevel is null || o.CefrLevel == cefrLevel.ToUpperInvariant())
                    && contextTags.Any(tag => o.ContextTagsJson.Contains($"\"{tag}\"")))
                .OrderBy(o => o.RecommendedOrder)
                .ToList();
            return Task.FromResult<IReadOnlyList<CurriculumObjective>>(results);
        }

        public Task<CurriculumObjective?> GetByKeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_objectives.FirstOrDefault(o => o.Key == key));
    }
}
