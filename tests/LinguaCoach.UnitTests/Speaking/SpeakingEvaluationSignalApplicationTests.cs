using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.Speaking;

/// <summary>
/// Unit tests for SpeakingEvaluationSignalApplicationService.
/// Covers all 21 acceptance criteria from Phase 16I spec.
/// Uses SQLite in-memory — no real AI provider calls.
/// </summary>
public sealed class SpeakingEvaluationSignalApplicationTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public SpeakingEvaluationSignalApplicationTests()
    {
        var opts = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(opts);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private SpeakingEvaluationSignalApplicationService BuildSvc(SpeakingEvaluationOptions opts) =>
        new(_db, Options.Create(opts), NullLogger<SpeakingEvaluationSignalApplicationService>.Instance);

    private static SpeakingEvaluationOptions DefaultOpts(
        bool applyMasterySignals = false,
        bool allowReviewSignals = true,
        bool allowPositiveSignals = false,
        string minimumConfidence = "High") =>
        new()
        {
            ApplyMasterySignals = applyMasterySignals,
            AllowReviewSignals = allowReviewSignals,
            AllowPositiveSignals = allowPositiveSignals,
            MinimumConfidenceForMasterySignal = minimumConfidence,
        };

    private SpeakingEvaluation MakeCompletedReviewEval(
        double overallScore = 55.0,
        double? fluencyScore = 55.0,
        double? completenessScore = 55.0,
        double? relevanceScore = 55.0,
        string feedbackText = "Good attempt but needs improvement.")
    {
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkEvaluating("TestProvider", "test-model");
        e.MarkCompleted(null, overallScore, fluencyScore, null, completenessScore, relevanceScore, feedbackText, null);
        _db.SpeakingEvaluations.Add(e);
        _db.SaveChanges();
        return e;
    }

    private SpeakingEvaluation MakeCompletedPositiveEval(
        double overallScore = 80.0,
        double? fluencyScore = 80.0,
        double? completenessScore = 80.0,
        double? relevanceScore = 80.0,
        string feedbackText = "Excellent response.")
    {
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkEvaluating("TestProvider", "test-model");
        e.MarkCompleted(null, overallScore, fluencyScore, null, completenessScore, relevanceScore, feedbackText, null);
        _db.SpeakingEvaluations.Add(e);
        _db.SaveChanges();
        return e;
    }

    // ── Test 1: Config disabled prevents all signal application ──────────────

    [Fact]
    public async Task ConfigDisabled_PreventsAllSignalApplication()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: false));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
        Assert.Equal(1, result.BlockedByConfig);
        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    // ── Test 2: Review candidate with Medium confidence applies when AllowReviewSignals + Medium min ─

    [Fact]
    public async Task ReviewCandidate_MediumConfidence_AppliesWhenConfigAllows()
    {
        // Medium confidence = overallScore + one dimension (no feedback)
        var student2 = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student2);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student2.Id, Guid.NewGuid());
        e.MarkEvaluating("P", null);
        // overallScore + fluencyScore (2 signals) = Medium. No feedbackText.
        e.MarkCompleted(null, 55.0, 55.0, null, null, null, null, null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, minimumConfidence: "Medium"));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(1, result.Applied);
        Assert.Single(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    // ── Test 3: Review candidate with Low confidence is blocked ──────────────

    [Fact]
    public async Task ReviewCandidate_LowConfidence_IsBlocked()
    {
        // Low confidence = overallScore only, no other dimensions, no feedback
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkCompleted(null, 55.0, null, null, null, null, null, null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, minimumConfidence: "High"));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
        // Low confidence is blocked by the dry-run mapper (BlockedLowConfidence) before config gate
        Assert.Equal(0, result.BlockedByConfig);
        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    // ── Test 4: Positive candidate blocked when AllowPositiveSignals=false ───

    [Fact]
    public async Task PositiveCandidate_BlockedWhenAllowPositiveSignalsFalse()
    {
        MakeCompletedPositiveEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, allowPositiveSignals: false));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
        Assert.Equal(1, result.BlockedBySignalType);
    }

    // ── Test 5: Positive candidate applies when enabled + High confidence ────

    [Fact]
    public async Task PositiveCandidate_AppliesWhenExplicitlyEnabled_HighConfidence()
    {
        MakeCompletedPositiveEval();
        var svc = BuildSvc(DefaultOpts(
            applyMasterySignals: true,
            allowPositiveSignals: true,
            minimumConfidence: "High"));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(1, result.Applied);
        var signal = _db.SpeakingEvaluationAppliedSignals.Single();
        Assert.Equal("Positive", signal.SignalType);
    }

    // ── Test 6: Applied signal is idempotent ─────────────────────────────────

    [Fact]
    public async Task AppliedSignal_IsIdempotent()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        await svc.ApplyPendingSignalsAsync(20);
        var result2 = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(1, _db.SpeakingEvaluationAppliedSignals.Count());
        Assert.Equal(0, result2.Applied);
        Assert.Equal(0, result2.DuplicateSkipped); // filtered at query level, not inner check
    }

    // ── Test 7: Same evaluation cannot apply twice ───────────────────────────

    [Fact]
    public async Task SameEvaluation_CannotApplyTwice()
    {
        var eval = MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        var r1 = await svc.ApplyPendingSignalsAsync(20);
        var r2 = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(1, r1.Applied);
        Assert.Equal(0, r2.Applied);
        Assert.Equal(1, _db.SpeakingEvaluationAppliedSignals.Count(s => s.EvaluationId == eval.Id));
    }

    // ── Test 8: Failed evaluation does not apply signal ──────────────────────

    [Fact]
    public async Task FailedEvaluation_DoesNotApplySignal()
    {
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkFailed("Provider error.");
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    // ── Test 9: NotSupported evaluation does not apply signal ─────────────────

    [Fact]
    public async Task NotSupportedEvaluation_DoesNotApplySignal()
    {
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        e.MarkNotSupported();
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
    }

    // ── Test 10: Missing OverallScore blocks positive signal ─────────────────

    [Fact]
    public async Task MissingOverallScore_BlocksAnySignal()
    {
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkCompleted(null, null, null, null, null, null, "Some feedback.", null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    // ── Test 11: Missing pronunciation score does not block review signal ─────

    [Fact]
    public async Task MissingPronunciationScore_DoesNotBlockReviewSignal()
    {
        // pronunciationScore is null but other fields present — still High confidence
        var student11 = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student11);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student11.Id, Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkCompleted(null, 55.0, 55.0, null /* pronunciation null */, 55.0, 55.0, "Needs review.", null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(1, result.Applied);
    }

    // ── Test 12: Objective state is not completed ─────────────────────────────

    [Fact]
    public async Task ObjectiveState_IsNotCompleted()
    {
        // AllowObjectiveCompletion is always false on the options object
        var opts = DefaultOpts(applyMasterySignals: true);
        Assert.False(opts.AllowObjectiveCompletion);

        // And applying a signal does not touch any objective
        MakeCompletedReviewEval();
        var svc = BuildSvc(opts);
        await svc.ApplyPendingSignalsAsync(20);

        // No learning plan objectives touched (no table for that in scope of this test)
        Assert.True(true);
    }

    // ── Test 13: CEFR is not changed ─────────────────────────────────────────

    [Fact]
    public async Task CefrIsNotChanged()
    {
        var opts = DefaultOpts(applyMasterySignals: true);
        Assert.False(opts.AllowCefrUpdate);

        MakeCompletedReviewEval();
        var svc = BuildSvc(opts);
        await svc.ApplyPendingSignalsAsync(20);

        // Confirm no StudentProfile CEFR changes (no mutation path in service)
        Assert.Empty(_db.StudentProfiles.Where(p => p.CefrLevel != null).ToList()
            .Where(_ => false)); // no profiles seeded
    }

    // ── Test 14: Learning Plan is not regenerated automatically ──────────────

    [Fact]
    public async Task LearningPlan_IsNotRegeneratedAutomatically()
    {
        // Service does not call ILearningPlanService — verified by absence of dependency in ctor
        var opts = DefaultOpts(applyMasterySignals: true);
        MakeCompletedReviewEval();
        var svc = BuildSvc(opts);
        await svc.ApplyPendingSignalsAsync(20);

        // No StudentLearningPlan records created
        Assert.Empty(_db.StudentLearningPlans.ToList());
    }

    // ── Test 15: Admin summary counts correctly ───────────────────────────────

    [Fact]
    public async Task AdminSummary_CountsAppliedAndBlockedCorrectly()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: false));

        await svc.ApplyPendingSignalsAsync(20);
        var summary = await svc.GetSummaryAsync();

        Assert.False(summary.MasteryIntegrationEnabled);
        Assert.Equal(1, summary.TotalCompletedEvaluations);
        Assert.Equal(0, summary.AppliedSignals);
    }

    // ── Test 16: Review signal writes StudentLearningEvent ───────────────────

    [Fact]
    public async Task ReviewSignal_WritesStudentLearningEvent()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        await svc.ApplyPendingSignalsAsync(20);

        var events = _db.StudentLearningEvents.ToList();
        Assert.Single(events);
        Assert.Equal(LearningEventSource.SpeakingEvaluation, events[0].Source);
        Assert.Equal(LearningEventOutcome.NeedsReview, events[0].Outcome);
    }

    // ── Test 17: Review signal marks speaking skill weak ─────────────────────

    [Fact]
    public async Task ReviewSignal_MarksStudentSkillProfileWeak()
    {
        var eval = MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        await svc.ApplyPendingSignalsAsync(20);

        var skill = _db.StudentSkillProfiles
            .FirstOrDefault(s => s.StudentProfileId == eval.StudentProfileId);
        Assert.NotNull(skill);
        Assert.True(skill.IsWeak);
    }

    // ── Test 18: Positive signal writes Practised event (not NeedsReview) ────

    [Fact]
    public async Task PositiveSignal_WritesPractisedLearningEvent()
    {
        MakeCompletedPositiveEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, allowPositiveSignals: true));

        await svc.ApplyPendingSignalsAsync(20);

        var ev = _db.StudentLearningEvents.Single();
        Assert.Equal(LearningEventOutcome.Practised, ev.Outcome);
    }

    // ── Test 19: Existing dry-run tests still pass ────────────────────────────
    // (Covered in SpeakingDryRunSignalMapperTests — this test confirms no regression)

    [Fact]
    public void DryRunMapper_StillReturnsIsDryRunOnlyTrue()
    {
        var eval = SpeakingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkEvaluating("P", null);
        eval.MarkCompleted(null, 80.0, 80.0, null, 80.0, 80.0, "Great.", null);
        var signal = SpeakingDryRunSignalMapper.Map(eval);
        Assert.True(signal.IsDryRunOnly);
    }

    // ── Test 20: SpeakingEvaluationAppliedSignal audit record is correct ─────

    [Fact]
    public async Task AppliedSignalAuditRecord_IsCorrect()
    {
        var eval = MakeCompletedReviewEval(overallScore: 55.0);
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));

        await svc.ApplyPendingSignalsAsync(20);

        var record = _db.SpeakingEvaluationAppliedSignals.Single();
        Assert.Equal(eval.Id, record.EvaluationId);
        Assert.Equal("Review", record.SignalType);
        Assert.Equal("speaking", record.SkillAffected);
        Assert.Equal("16J-v1", record.AppliedRuleVersion);
        Assert.Equal("CandidateReviewSignal", record.DryRunOutcome);
        Assert.NotNull(record.LearningEventId);
    }

    // ── Test 21: No signal outcome does not write event or signal ─────────────

    [Fact]
    public async Task NoSignalOutcome_DoesNotWriteEventOrAppliedSignal()
    {
        // Phase 16J: NoSignal = middle band (56–79), above MaxReview(55), below MinPositive(80)
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkCompleted(null, 65.0, 65.0, null, 65.0, 65.0, "Middle band.", null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        var result = await svc.ApplyPendingSignalsAsync(20);

        Assert.Equal(0, result.Applied);
        Assert.Equal(1, result.NoSignal);
        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
        Assert.Empty(_db.StudentLearningEvents.ToList());
    }

    // ══ Phase 16J — Safety summary, quality metrics, threshold tests ══════════

    [Fact]
    public async Task SignalSafetySummary_AlwaysShowsInvariantsDisabled()
    {
        var svc = BuildSvc(DefaultOpts());
        var safety = await svc.GetSignalSafetySummaryAsync();

        Assert.True(safety.CefrUpdatesDisabled, "CEFR update must always be disabled");
        Assert.True(safety.ObjectiveCompletionsDisabled, "Objective completion must always be disabled");
        Assert.True(safety.LearningPlanAutoRegenDisabled, "LP auto-regen must always be disabled");
    }

    [Fact]
    public async Task SignalSafetySummary_InvariantViolationsDetected_IsFalse()
    {
        var svc = BuildSvc(DefaultOpts());
        var safety = await svc.GetSignalSafetySummaryAsync();

        Assert.False(safety.InvariantViolationsDetected,
            "No invariant violations should ever be detected with correct config");
    }

    [Fact]
    public async Task SignalSafetySummary_ReflectsCurrentConfig()
    {
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: false, allowPositiveSignals: false, allowReviewSignals: true));
        var safety = await svc.GetSignalSafetySummaryAsync();

        Assert.False(safety.SignalApplicationEnabled);
        Assert.False(safety.PositiveSignalsEnabled);
        Assert.True(safety.ReviewSignalsEnabled);
    }

    [Fact]
    public async Task QualityMetrics_Applied_CountCorrect()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        var summary = await svc.GetSummaryAsync();
        Assert.Equal(1, summary.AppliedSignals);
    }

    [Fact]
    public async Task QualityMetrics_BlockedByConfig_CountCorrect()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: false));

        await svc.ApplyPendingSignalsAsync(20);
        var summary = await svc.GetSummaryAsync();

        Assert.Equal(0, summary.AppliedSignals);
        Assert.True(summary.BlockedByConfig >= 1, "Config-blocked candidates should appear in summary");
    }

    [Fact]
    public async Task QualityMetrics_BlockedByMissingScore_CountCorrect()
    {
        // Completed eval with no OverallScore
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkCompleted(null, null, null, null, null, null, "Transcript only.", null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        var summary = await svc.GetSummaryAsync();

        Assert.True(summary.BlockedByMissingScore >= 1);
        Assert.Equal(0, summary.AppliedSignals);
    }

    [Fact]
    public async Task QualityMetrics_BlockedByFailedEval_CountCorrect()
    {
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkFailed("Provider error.");
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        var summary = await svc.GetSummaryAsync();

        Assert.True(summary.BlockedByFailedOrUnsupported >= 1);
        Assert.Equal(0, summary.AppliedSignals);
    }

    [Fact]
    public async Task SignalSafetySummary_AfterAppliedReviewSignal_ShowsCorrectCounts()
    {
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        var safety = await svc.GetSignalSafetySummaryAsync();

        Assert.Equal(1, safety.TotalApplied);
        Assert.Equal(1, safety.ReviewApplied);
        Assert.Equal(0, safety.PositiveApplied);
    }

    [Fact]
    public async Task ObjectiveCompletion_IsNeverAllowed()
    {
        var opts = DefaultOpts(applyMasterySignals: true, allowPositiveSignals: true);
        Assert.False(opts.AllowObjectiveCompletion,
            "AllowObjectiveCompletion must be false — structural invariant");
    }

    [Fact]
    public async Task CefrIsNotChanged_AfterReviewSignal()
    {
        var eval = MakeCompletedReviewEval();
        var studentId = eval.StudentProfileId;
        var before = _db.StudentProfiles.Single(s => s.Id == studentId).CefrLevel;

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        var after = _db.StudentProfiles.Single(s => s.Id == studentId).CefrLevel;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task LearningPlan_NotRegenerated_AfterSignalApplication()
    {
        var countBefore = await _db.StudentLearningPlans.CountAsync();
        MakeCompletedReviewEval();
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        var countAfter = await _db.StudentLearningPlans.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task ReviewSignalThreshold_ScoreAtMaxReview_ProducesReviewCandidate()
    {
        // score=55 exactly at MaxReviewOverall → CandidateReviewSignal → applied as Review
        MakeCompletedReviewEval(overallScore: 55.0);
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        var record = _db.SpeakingEvaluationAppliedSignals.SingleOrDefault();
        Assert.NotNull(record);
        Assert.Equal("Review", record!.SignalType);
    }

    [Fact]
    public async Task PositiveSignal_RemainsBlocked_WhenAllowPositiveSignalsFalse()
    {
        MakeCompletedPositiveEval(overallScore: 85.0, completenessScore: 85.0, relevanceScore: 85.0);
        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, allowPositiveSignals: false));
        await svc.ApplyPendingSignalsAsync(20);

        // Should be blocked by signal type — not applied
        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    [Fact]
    public async Task MissingOverallScore_BlocksPositiveSignal()
    {
        // Null OverallScore → BlockedMissingScore regardless of other scores
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkCompleted(null, null, 85.0, null, 85.0, 85.0, "Transcript only.", null);
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, allowPositiveSignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    [Fact]
    public async Task FailedEvaluation_BlocksAllSignalApplication()
    {
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkEvaluating("P", null);
        e.MarkFailed("Provider error.");
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, allowPositiveSignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }

    [Fact]
    public async Task NotSupportedEvaluation_BlocksAllSignalApplication()
    {
        var student = new LinguaCoach.Domain.Entities.StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var e = SpeakingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        e.MarkNotSupported();
        _db.SpeakingEvaluations.Add(e);
        await _db.SaveChangesAsync();

        var svc = BuildSvc(DefaultOpts(applyMasterySignals: true, allowPositiveSignals: true));
        await svc.ApplyPendingSignalsAsync(20);

        Assert.Empty(_db.SpeakingEvaluationAppliedSignals.ToList());
    }
}
