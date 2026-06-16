using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Memory;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// Integration tests for SessionGeneratorService against an in-process SQLite database.
/// Tests cover: idempotency, duration templates, weak-skill substitution, no-path guard,
/// duplicate session prevention.
/// </summary>
public sealed class SessionGeneratorServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SessionGeneratorService _service;

    public SessionGeneratorServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        ExerciseTypeDefinitionSeeder.SeedAsync(_db, NullLogger.Instance).GetAwaiter().GetResult();

        var ledger = new StudentLearningLedgerService(_db, NullLogger<StudentLearningLedgerService>.Instance);
        _service = new SessionGeneratorService(_db, new FakeLearningPathGenerator(_db), new ExerciseTypeRegistry(_db), ledger, NullLogger<SessionGeneratorService>.Instance);
    }

    /// <summary>
    /// Stands in for AiLearningPathGeneratorHandler: creates a minimal active LearningPath
    /// with one module for the given user's StudentProfile, so SessionGeneratorService's
    /// lazy-generation fallback can be exercised without AI.
    /// </summary>
    private sealed class FakeLearningPathGenerator(LinguaCoachDbContext db) : ILearningPathGenerator
    {
        public async Task<LearningPathDto> GenerateAsync(GenerateLearningPathCommand command, CancellationToken ct = default)
        {
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == command.UserId, ct);

            var path = new LearningPath(profile.Id, "Workplace English", "Lazily generated for tests");
            db.LearningPaths.Add(path);
            await db.SaveChangesAsync(ct);

            var module = new LearningModule(path.Id, "Professional Communication", "Core workplace skills", order: 0);
            db.LearningModules.Add(module);
            await db.SaveChangesAsync(ct);

            return new LearningPathDto(path.Id, path.Title, path.IsActive, null, 0, 1, []);
        }
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a minimal CourseReady student with a LearningPath and one module.</summary>
    private async Task<(StudentProfile profile, LearningModule module)> SeedCourseReadyStudentAsync(
        int? preferredDuration = 15,
        SkillFocus skillFocus = SkillFocus.Writing)
    {
        var student = new StudentProfile(Guid.NewGuid());
        student.SetLifecycleStage(StudentLifecycleStage.CourseReady);
        // Use UpdateAdminProfile to set duration without triggering onboarding state machine
        student.SetInitialProfile(
            firstName: "Test",
            lastName: "Student",
            displayName: null,
            careerContext: "Document Controller",
            learningGoal: null,
            preferredSessionDurationMinutes: preferredDuration,
            experienceLevel: ProfessionalExperienceLevel.EntryLevelOrGraduate,
            roleFamiliarity: RoleFamiliarity.NewToRole);
        _db.StudentProfiles.Add(student);
        await _db.SaveChangesAsync();

        var path = new LearningPath(student.Id, "Workplace English", "Adaptive path");
        _db.LearningPaths.Add(path);
        await _db.SaveChangesAsync();

        var module = new LearningModule(path.Id, "Professional Communication", "Core workplace skills", order: 0);
        _db.LearningModules.Add(module);
        await _db.SaveChangesAsync();

        return (student, module);
    }

    private static GetOrCreateTodaysSessionCommand CommandFor(StudentProfile profile)
        => new(profile.Id);

    // ── Core behaviour ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreate_CreatesSessionWithExercises()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
        Assert.Equal(15, result.DurationMinutes);
        Assert.NotEmpty(result.Exercises);
        Assert.False(result.IsResuming);
    }

    [Fact]
    public async Task GetOrCreate_CalledTwice_ReturnsSameSession()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();

        var first = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        var second = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.Equal(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task GetOrCreate_UsesReadyBufferedSessionBeforeGenerating()
    {
        var (profile, module) = await SeedCourseReadyStudentAsync();
        var buffered = new LearningSession(
            module.Id,
            "Cached vocabulary lesson",
            "Workplace vocabulary",
            "Learn useful workplace words.",
            15,
            "vocabulary",
            order: 0);
        buffered.SetGenerationMetadata(profile.Id, 1, generationBatchId: null);
        buffered.MarkGenerationReady();
        _db.LearningSessions.Add(buffered);
        await _db.SaveChangesAsync();

        var exercise = new SessionExercise(
            buffered.Id,
            0,
            "phrase_match",
            "vocabulary",
            "Learn the phrases, then match them to meanings.",
            5);
        _db.SessionExercises.Add(exercise);
        await _db.SaveChangesAsync();

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.Equal(buffered.Id, result.SessionId);
        Assert.Equal("Cached vocabulary lesson", result.Title);
        Assert.Single(result.Exercises);
        Assert.Equal(ExerciseKind.VocabularyWarmup, result.Exercises[0].Kind);
    }

    [Fact]
    public async Task GetOrCreate_NoDuplicateSessionsCreated()
    {
        var (profile, module) = await SeedCourseReadyStudentAsync();

        await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        var sessionCount = await _db.LearningSessions
            .CountAsync(s => s.LearningModuleId == module.Id);

        Assert.Equal(1, sessionCount);
    }

    [Fact]
    public async Task GetOrCreate_NoLearningPath_GeneratesPathLazilyAndCreatesSession()
    {
        var student = new StudentProfile(Guid.NewGuid());
        student.SetLifecycleStage(StudentLifecycleStage.CourseReady);
        _db.StudentProfiles.Add(student);
        await _db.SaveChangesAsync();

        var result = await _service.GetOrCreateTodaysSessionAsync(new GetOrCreateTodaysSessionCommand(student.Id));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.Exercises);
        Assert.True(await _db.LearningPaths.AnyAsync(p => p.StudentProfileId == student.Id && p.IsActive));
    }

    [Fact]
    public async Task GetOrCreate_StudentNotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetOrCreateTodaysSessionAsync(new GetOrCreateTodaysSessionCommand(Guid.NewGuid())));
    }

    // ── Duration templates ────────────────────────────────────────────────────

    [Fact]
    public async Task Duration10_ProducesThreeExercises()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 10);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(3, result.Exercises.Count);
    }

    [Fact]
    public async Task Duration15_ProducesFourExercises()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(4, result.Exercises.Count);
    }

    [Fact]
    public async Task Duration20_ProducesFourExercises()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 20);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(4, result.Exercises.Count);
    }

    [Fact]
    public async Task Duration30_ProducesFiveExercises()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 30);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(5, result.Exercises.Count);
    }

    [Fact]
    public async Task NullDuration_FallsBackToDefaultFifteenMinTemplate()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: null);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        // Default is 15 min → 4 steps
        Assert.Equal(4, result.Exercises.Count);
        Assert.Equal(15, result.DurationMinutes);
    }

    // ── Exercise ordering ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task Exercises_AreZeroIndexedAndContiguous(int duration)
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: duration);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        for (var i = 0; i < result.Exercises.Count; i++)
            Assert.Equal(i, result.Exercises[i].Order);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task FirstExercise_IsVocabularyWarmup(int duration)
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: duration);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(ExerciseKind.VocabularyWarmup, result.Exercises[0].Kind);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task LastExercise_IsReview(int duration)
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: duration);
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(ExerciseKind.Review, result.Exercises[^1].Kind);
    }

    // ── Exercise initial state ────────────────────────────────────────────────

    [Fact]
    public async Task AllExercises_StartNotStarted()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.All(result.Exercises, e => Assert.Equal(ExerciseStatus.NotStarted, e.Status));
    }

    [Fact]
    public async Task AllExercises_HaveNullLearningActivityId()
    {
        // Phase 2 does not generate activities — that is Phase 3.
        var (profile, _) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.All(result.Exercises, e => Assert.Null(e.LearningActivityId));
    }

    // ── Session initial state ─────────────────────────────────────────────────

    [Fact]
    public async Task NewSession_StatusIsNotStarted()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(SessionStatus.NotStarted, result.Status);
    }

    [Fact]
    public async Task ResumedSession_IsResumingTrue()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();

        // Create and start the session manually
        var first = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        var session = await _db.LearningSessions.FindAsync(first.SessionId);
        session!.Start();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Generator should return the in-progress session
        var resumed = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.Equal(first.SessionId, resumed.SessionId);
        Assert.True(resumed.IsResuming);
    }

    // ── Weak-skill substitution ────────────────────────────────────────────────

    [Fact]
    public async Task WeakSpeakingSkill_PromotesSpeakingTaskInMainSlot()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        // Mark Speaking as weak
        _db.StudentSkillProfiles.Add(
            new StudentSkillProfile(profile.Id, "speaking", "Speaking", isWeak: true));
        await _db.SaveChangesAsync();

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        // At least one exercise should be SpeakingTask
        Assert.Contains(result.Exercises, e => e.Kind == ExerciseKind.SpeakingTask);
    }

    [Fact]
    public async Task WeakListeningSkill_EnsuresListeningInputStep()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        _db.StudentSkillProfiles.Add(
            new StudentSkillProfile(profile.Id, "listening", "Listening", isWeak: true));
        await _db.SaveChangesAsync();

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.Contains(result.Exercises, e => e.Kind == ExerciseKind.ListeningInput);
    }

    [Fact]
    public async Task NoWeakSkills_DefaultTemplateUsed_ContainsWritingTask()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.Contains(result.Exercises, e => e.Kind == ExerciseKind.WritingTask);
    }

    // ── Session metadata ──────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratedSession_HasNonEmptyTitle()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
    }

    [Fact]
    public async Task GeneratedSession_HasNonEmptySessionGoal()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.False(string.IsNullOrWhiteSpace(result.SessionGoal));
    }

    [Fact]
    public async Task GeneratedSession_AllExercisesHaveNonEmptyInstructions()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        Assert.All(result.Exercises, e => Assert.False(string.IsNullOrWhiteSpace(e.Instructions)));
    }

    // ── Module progression ────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratedSession_IsLinkedToCurrentModule()
    {
        var (profile, module) = await SeedCourseReadyStudentAsync();
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        var session = await _db.LearningSessions.FindAsync(result.SessionId);
        Assert.Equal(module.Id, session!.LearningModuleId);
    }

    // ── Ledger-aware selection (10C) ──────────────────────────────────────────

    [Fact]
    public async Task LedgerWeakEvent_DoesNotCrash_AndSessionIsGenerated()
    {
        // Seeds a NeedsReview ledger event for a pattern, then verifies session
        // generation still completes without error. Ledger data is advisory.
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        // Seed a weak ledger event for a writing pattern.
        var weakEvent = new StudentLearningEvent(
            studentProfileId: profile.Id,
            source: LearningEventSource.TodayLesson,
            outcome: LearningEventOutcome.NeedsReview,
            patternKey: "email_reply",
            primarySkill: "writing");
        _db.StudentLearningEvents.Add(weakEvent);
        await _db.SaveChangesAsync();

        // Session generation must succeed with ledger data present.
        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.Exercises);
    }

    [Fact]
    public async Task LedgerMasteredEvent_DoesNotCrash_AndSessionIsGenerated()
    {
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        var masteredEvent = new StudentLearningEvent(
            studentProfileId: profile.Id,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Mastered,
            patternKey: "phrase_match",
            primarySkill: "vocabulary");
        _db.StudentLearningEvents.Add(masteredEvent);
        await _db.SaveChangesAsync();

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.Exercises);
    }

    [Fact]
    public async Task NoLedgerEvents_GeneratesSessionIdenticallyTo10A()
    {
        // No ledger events at all — session generation must succeed (10A fallback).
        var (profile, _) = await SeedCourseReadyStudentAsync(preferredDuration: 15);

        var result = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.Exercises);
    }

    [Fact]
    public async Task CompletedSessions_CountTowardModuleProgression()
    {
        var (profile, module) = await SeedCourseReadyStudentAsync();

        // Create + complete 5 sessions on the first module to exhaust it
        for (var i = 0; i < 5; i++)
        {
            var sessionResult = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
            var session = await _db.LearningSessions.FindAsync(sessionResult.SessionId);
            session!.Start();
            session.Complete();
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        // Add a second module
        var path = await _db.LearningPaths.FirstAsync(p => p.StudentProfileId == profile.Id);
        var module2 = new LearningModule(path.Id, "Advanced Communication", "Complex scenarios", order: 1);
        _db.LearningModules.Add(module2);
        await _db.SaveChangesAsync();

        // Next session should use module 2
        var nextResult = await _service.GetOrCreateTodaysSessionAsync(CommandFor(profile));
        var nextSession = await _db.LearningSessions.FindAsync(nextResult.SessionId);
        Assert.Equal(module2.Id, nextSession!.LearningModuleId);
    }
}
