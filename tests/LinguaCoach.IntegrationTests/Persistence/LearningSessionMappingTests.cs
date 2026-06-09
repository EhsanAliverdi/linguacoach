using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Persistence smoke tests for LearningSession and SessionExercise against
/// an in-process SQLite database. Validates schema creation, FK wiring, status
/// transitions, and round-trip persistence.
/// </summary>
public sealed class LearningSessionMappingTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public LearningSessionMappingTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private LearningPath SeedPath()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();

        var path = new LearningPath(student.Id, "Workplace English", "Adaptive path for Document Controller");
        _db.LearningPaths.Add(path);
        _db.SaveChanges();
        return path;
    }

    private LearningModule SeedModule(Guid pathId)
    {
        var module = new LearningModule(pathId, "Professional Communication", "Core workplace skills", order: 0);
        _db.LearningModules.Add(module);
        _db.SaveChanges();
        return module;
    }

    private LearningSession SeedSession(Guid moduleId, int order = 0)
    {
        var session = new LearningSession(
            learningModuleId: moduleId,
            title: "Explaining a Delay Professionally",
            topic: "Professional delay communication",
            sessionGoal: "Write a professional delay notification message",
            durationMinutes: 15,
            focusSkill: "Writing",
            order: order);
        _db.LearningSessions.Add(session);
        _db.SaveChanges();
        return session;
    }

    // ── LearningSession ───────────────────────────────────────────────────────

    [Fact]
    public void LearningSession_CanBeSavedAndLoaded()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        _db.ChangeTracker.Clear();
        var loaded = _db.LearningSessions.Single(s => s.Id == session.Id);

        Assert.Equal("Explaining a Delay Professionally", loaded.Title);
        Assert.Equal("Professional delay communication", loaded.Topic);
        Assert.Equal(15, loaded.DurationMinutes);
        Assert.Equal("Writing", loaded.FocusSkill);
        Assert.Equal(SessionStatus.NotStarted, loaded.Status);
        Assert.Equal(0, loaded.Order);
        Assert.Null(loaded.StartedAtUtc);
        Assert.Null(loaded.CompletedAtUtc);
        Assert.Null(loaded.SecondarySkillsJson);
        Assert.Null(loaded.GeneratedFromMemorySnapshotJson);
    }

    [Fact]
    public void LearningSession_WithOptionalFields_PersistedCorrectly()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);

        var session = new LearningSession(
            module.Id, "Title", "Topic", "Goal", 20, "Listening", 1,
            secondarySkillsJson: """["Vocabulary","Grammar"]""",
            generatedFromMemorySnapshotJson: """{"cefrLevel":"B1"}""");
        _db.LearningSessions.Add(session);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.LearningSessions.Single(s => s.Id == session.Id);
        Assert.Equal("""["Vocabulary","Grammar"]""", loaded.SecondarySkillsJson);
        Assert.Equal("""{"cefrLevel":"B1"}""", loaded.GeneratedFromMemorySnapshotJson);
    }

    [Fact]
    public void LearningSession_StatusTransition_PersistedCorrectly()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        // Start
        session.Start();
        _db.LearningSessions.Update(session);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var afterStart = _db.LearningSessions.Single(s => s.Id == session.Id);
        Assert.Equal(SessionStatus.InProgress, afterStart.Status);
        Assert.NotNull(afterStart.StartedAtUtc);
        Assert.Null(afterStart.CompletedAtUtc);

        // Complete
        afterStart.Complete();
        _db.LearningSessions.Update(afterStart);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var afterComplete = _db.LearningSessions.Single(s => s.Id == session.Id);
        Assert.Equal(SessionStatus.Completed, afterComplete.Status);
        Assert.NotNull(afterComplete.CompletedAtUtc);
    }

    [Fact]
    public void LearningSession_CascadeDelete_RemovesExercises()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        var exercise = new SessionExercise(
            session.Id, 0, "listen_and_gap_fill", "Listening", "Fill in the gaps.", 4);
        _db.SessionExercises.Add(exercise);
        _db.SaveChanges();

        _db.LearningSessions.Remove(session);
        _db.SaveChanges();

        Assert.Equal(0, _db.SessionExercises.Count(e => e.LearningSessionId == session.Id));
    }

    // ── SessionExercise ───────────────────────────────────────────────────────

    [Fact]
    public void SessionExercise_CanBeSavedAndLoaded()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        var exercise = new SessionExercise(
            session.Id, 0, "listen_and_gap_fill", "Listening",
            "Listen and fill in the gaps.", 4,
            secondarySkillsJson: """["Vocabulary"]""");
        _db.SessionExercises.Add(exercise);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.SessionExercises.Single(e => e.Id == exercise.Id);
        Assert.Equal(session.Id, loaded.LearningSessionId);
        Assert.Equal(0, loaded.Order);
        Assert.Equal("listen_and_gap_fill", loaded.ExercisePatternKey);
        Assert.Equal("Listening", loaded.PrimarySkill);
        Assert.Equal(4, loaded.EstimatedMinutes);
        Assert.Equal(ExerciseStatus.NotStarted, loaded.Status);
        Assert.Null(loaded.LearningActivityId);
        Assert.Null(loaded.CompletedAtUtc);
        Assert.Equal("""["Vocabulary"]""", loaded.SecondarySkillsJson);
    }

    [Fact]
    public void SessionExercise_StatusTransition_PersistedCorrectly()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        var exercise = new SessionExercise(session.Id, 0, "key", "Listening", "Instr", 3);
        _db.SessionExercises.Add(exercise);
        _db.SaveChanges();

        exercise.Start();
        _db.SessionExercises.Update(exercise);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var afterStart = _db.SessionExercises.Single(e => e.Id == exercise.Id);
        Assert.Equal(ExerciseStatus.InProgress, afterStart.Status);

        afterStart.Complete();
        _db.SessionExercises.Update(afterStart);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var afterComplete = _db.SessionExercises.Single(e => e.Id == exercise.Id);
        Assert.Equal(ExerciseStatus.Completed, afterComplete.Status);
        Assert.NotNull(afterComplete.CompletedAtUtc);
    }

    [Fact]
    public void SessionExercise_AssignActivity_PersistedCorrectly()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        var activity = new LearningActivity(
            ActivityType.ListeningComprehension, ActivitySource.AiGenerated,
            "Listening exercise", "B1", "{}",
            learningModuleId: module.Id);
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();

        var exercise = new SessionExercise(session.Id, 0, "listen_and_answer", "Listening", "Instr", 4);
        _db.SessionExercises.Add(exercise);
        _db.SaveChanges();

        exercise.AssignActivity(activity.Id);
        _db.SessionExercises.Update(exercise);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.SessionExercises.Single(e => e.Id == exercise.Id);
        Assert.Equal(activity.Id, loaded.LearningActivityId);
    }

    [Fact]
    public void SessionExercise_MicroLessonStep_HasNullActivityId()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        // Micro-lesson steps never have a LearningActivity linked
        var microStep = new SessionExercise(
            session.Id, 0, "micro_lesson_phrases", "Vocabulary",
            "Read through these target phrases before the next exercise.", 2);
        _db.SessionExercises.Add(microStep);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.SessionExercises.Single(e => e.Id == microStep.Id);
        Assert.Null(loaded.LearningActivityId);
        Assert.Equal("micro_lesson_phrases", loaded.ExercisePatternKey);
    }

    [Fact]
    public void SessionExercise_OrderedWithinSession_IndexExists()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        // Add exercises in reverse order to confirm ordering is explicit
        for (var i = 4; i >= 0; i--)
        {
            _db.SessionExercises.Add(new SessionExercise(
                session.Id, i, "key", "Listening", "Instr", 3));
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var exercises = _db.SessionExercises
            .Where(e => e.LearningSessionId == session.Id)
            .OrderBy(e => e.Order)
            .ToList();

        Assert.Equal(5, exercises.Count);
        Assert.Equal(0, exercises[0].Order);
        Assert.Equal(4, exercises[4].Order);
    }

    // ── Navigation: LearningSession.Exercises ─────────────────────────────────

    [Fact]
    public void LearningSession_Exercises_LoadedViaNavigation()
    {
        var path = SeedPath();
        var module = SeedModule(path.Id);
        var session = SeedSession(module.Id);

        _db.SessionExercises.Add(new SessionExercise(session.Id, 0, "k1", "Listening", "Instr", 3));
        _db.SessionExercises.Add(new SessionExercise(session.Id, 1, "k2", "Writing", "Instr", 5));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.LearningSessions
            .Include(s => s.Exercises)
            .Single(s => s.Id == session.Id);

        Assert.Equal(2, loaded.Exercises.Count);
    }
}
