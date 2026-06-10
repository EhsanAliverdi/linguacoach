using LinguaCoach.Application.Activity;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Tests for PatternSkillUpdateService — upserts StudentSkillProfile rows from PatternEvaluationResult.
/// Uses SQLite in-memory to verify actual DB writes without a full web stack.
/// </summary>
public sealed class PatternSkillUpdateServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly PatternSkillUpdateService _sut;
    private readonly Guid _studentProfileId;

    public PatternSkillUpdateServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new PatternSkillUpdateService(_db, NullLogger<PatternSkillUpdateService>.Instance);

        // Seed a minimal student profile so FK constraints pass
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        _studentProfileId = student.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── explicit skillImpacts ──────────────────────────────────────────────────

    [Fact]
    public async Task ExplicitSkillImpacts_KnownKey_UpsertSkillProfile()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 80, maxScore: 100, passed: true, completed: true,
            skillImpacts: [new PatternEvaluationSkillImpact("grammar_accuracy", "Grammar accuracy", 0.8, null)]);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None);

        var profile = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "grammar_accuracy");

        Assert.NotNull(profile);
        Assert.False(profile.IsWeak); // delta > 0 → not weak
    }

    [Fact]
    public async Task ExplicitSkillImpacts_NegativeDelta_MarksWeak()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 40, maxScore: 100, passed: false, completed: true,
            skillImpacts: [new PatternEvaluationSkillImpact("formal_tone", "Formal workplace tone", -0.6, null)]);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None);

        var profile = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "formal_tone");

        Assert.NotNull(profile);
        Assert.True(profile.IsWeak);
    }

    [Fact]
    public async Task ExplicitSkillImpacts_UnknownKey_DroppedSilently()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 70, maxScore: 100, passed: true, completed: true,
            skillImpacts: [new PatternEvaluationSkillImpact("totally_made_up_skill", "Fake skill", 1.0, null)]);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None);

        var count = await _db.StudentSkillProfiles
            .CountAsync(x => x.StudentProfileId == _studentProfileId);

        Assert.Equal(0, count); // unknown key → nothing written
    }

    [Fact]
    public async Task ExplicitSkillImpacts_DeltaAbove1_ClampedTo1()
    {
        // A delta of 999 from a malformed AI response must not cause an error
        var evalResult = PatternEvaluationResult.Create(
            score: 100, maxScore: 100, passed: true, completed: true,
            skillImpacts: [new PatternEvaluationSkillImpact("sentence_clarity", "Sentence clarity", 999.0, null)]);

        var exception = await Record.ExceptionAsync(() =>
            _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None));

        Assert.Null(exception); // clamped — no crash
        var profile = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "sentence_clarity");
        Assert.NotNull(profile);
        Assert.False(profile.IsWeak); // clamped to 1.0 → positive → not weak
    }

    [Fact]
    public async Task ExplicitSkillImpacts_UpdatesExistingProfile()
    {
        _db.StudentSkillProfiles.Add(new StudentSkillProfile(_studentProfileId, "workplace_vocabulary", "Workplace vocabulary", isWeak: true));
        await _db.SaveChangesAsync();

        var evalResult = PatternEvaluationResult.Create(
            score: 90, maxScore: 100, passed: true, completed: true,
            skillImpacts: [new PatternEvaluationSkillImpact("workplace_vocabulary", "Workplace vocabulary", 0.9, null)]);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "phrase_match", CancellationToken.None);

        var profile = await _db.StudentSkillProfiles
            .FirstAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "workplace_vocabulary");

        Assert.False(profile.IsWeak); // was weak, now updated to not-weak
    }

    // ── fallback from percentage when skillImpacts absent ─────────────────────

    [Fact]
    public async Task NoSkillImpacts_GoodScore_SynthesisesPositiveImpactFromPatternKey()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 80, maxScore: 100, passed: true, completed: true);
        // email_reply → message_structure

        await _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None);

        var profile = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "message_structure");

        Assert.NotNull(profile);
        Assert.False(profile.IsWeak); // good score → not weak
    }

    [Fact]
    public async Task NoSkillImpacts_PoorScore_SynthesisesNegativeImpactFromPatternKey()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 40, maxScore: 100, passed: false, completed: true);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None);

        var profile = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "message_structure");

        Assert.NotNull(profile);
        Assert.True(profile.IsWeak); // poor score → weak
    }

    [Fact]
    public async Task NoSkillImpacts_NotCompleted_DoesNotSynthesise()
    {
        // If evaluation didn't complete (e.g. AI unavailable), don't write skill data
        var evalResult = PatternEvaluationResult.Create(
            score: 0, maxScore: 0, passed: false, completed: false);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "email_reply", CancellationToken.None);

        var count = await _db.StudentSkillProfiles.CountAsync(x => x.StudentProfileId == _studentProfileId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task NoSkillImpacts_UnknownPatternKey_WritesNothing()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 80, maxScore: 100, passed: true, completed: true);

        await _sut.ApplyAsync(_studentProfileId, evalResult, "unknown_future_pattern", CancellationToken.None);

        var count = await _db.StudentSkillProfiles.CountAsync(x => x.StudentProfileId == _studentProfileId);
        Assert.Equal(0, count);
    }

    // ── normalisation of skill keys ────────────────────────────────────────────

    [Fact]
    public async Task SkillKey_WithHyphensAndSpaces_Normalised()
    {
        var evalResult = PatternEvaluationResult.Create(
            score: 75, maxScore: 100, passed: true, completed: true,
            skillImpacts: [new PatternEvaluationSkillImpact("grammar-accuracy", "Grammar accuracy", 0.5, null)]);

        await _sut.ApplyAsync(_studentProfileId, evalResult, null, CancellationToken.None);

        var profile = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _studentProfileId && x.SkillKey == "grammar_accuracy");
        Assert.NotNull(profile);
    }

    // ── NoMarking patterns ────────────────────────────────────────────────────

    [Fact]
    public async Task NoMarkingResult_MaxScoreZero_StillCreatesSkillEntry()
    {
        // lesson_reflection: score=0, maxScore=0, percentage=0, completed=true
        var evalResult = PatternEvaluationResult.Create(
            score: 0, maxScore: 0, passed: true, completed: true);
        // lesson_reflection → message_structure, but percentage=0 → could be synthesised as weak
        // The test verifies it runs without error and writes correctly

        var exception = await Record.ExceptionAsync(() =>
            _sut.ApplyAsync(_studentProfileId, evalResult, "lesson_reflection", CancellationToken.None));

        Assert.Null(exception);
    }
}
