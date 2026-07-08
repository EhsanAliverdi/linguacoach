using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Bugfix-D1A regression tests. Prior to this fix, <see cref="LearningSession.GenerationStatus"/>
/// was configured with EF <c>HasDefaultValue(GenerationStatus.Ready)</c>. Since
/// <see cref="GenerationStatus.Pending"/> is also the enum's CLR default (0), EF Core's
/// "omit CLR-default values from the INSERT, let the DB default apply" convention silently
/// discarded an explicit <see cref="LearningSession.MarkGenerationPending"/> call made before a
/// brand-new session's first <c>SaveChangesAsync</c> — the row always persisted as Ready
/// regardless. <see cref="LinguaCoach.Infrastructure.Jobs.LessonBatchGenerationJob"/> uses exactly
/// this construction order. These tests prove every <see cref="GenerationStatus"/> value now
/// round-trips correctly through a real save/reload cycle.
/// </summary>
public sealed class LearningSessionGenerationStatusPersistenceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public LearningSessionGenerationStatusPersistenceTests()
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

    private Guid SeedModuleId()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();

        var path = new LearningPath(student.Id, "Path", "General workplace English context.");
        _db.LearningPaths.Add(path);
        _db.SaveChanges();

        var module = new LearningModule(path.Id, "Module", "desc", 0);
        _db.LearningModules.Add(module);
        _db.SaveChanges();

        return module.Id;
    }

    private async Task<GenerationStatus> RoundTripAsync(Action<LearningSession> mutateBeforeFirstSave)
    {
        var moduleId = SeedModuleId();
        var session = new LearningSession(moduleId, "S", "topic", "goal", 15, "Vocabulary", 0);
        mutateBeforeFirstSave(session);

        _db.LearningSessions.Add(session);
        await _db.SaveChangesAsync();

        // Force a real round-trip through the database rather than reading the tracked instance —
        // this is the whole point of the regression test: prove what was actually persisted, not
        // what's still held in memory.
        var reloaded = await _db.LearningSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        return reloaded.GenerationStatus;
    }

    [Fact]
    public async Task Pending_status_set_before_first_save_persists_as_pending_after_reload()
    {
        var status = await RoundTripAsync(s => s.MarkGenerationPending());

        Assert.Equal(GenerationStatus.Pending, status);
    }

    [Fact]
    public async Task Ready_status_left_as_constructor_default_persists_as_ready_after_reload()
    {
        // No explicit call — GenerationStatus defaults to Ready via the property initializer,
        // matching every pre-D1 call site that never touches generation status at all
        // (e.g. SessionGeneratorService's on-demand path).
        var status = await RoundTripAsync(_ => { });

        Assert.Equal(GenerationStatus.Ready, status);
    }

    [Fact]
    public async Task Ready_status_explicitly_set_before_first_save_persists_as_ready_after_reload()
    {
        var status = await RoundTripAsync(s => s.MarkGenerationReady());

        Assert.Equal(GenerationStatus.Ready, status);
    }

    [Fact]
    public async Task Failed_status_set_before_first_save_persists_as_failed_after_reload()
    {
        var status = await RoundTripAsync(s => s.MarkGenerationFailed());

        Assert.Equal(GenerationStatus.Failed, status);
    }

    [Fact]
    public async Task Pending_status_set_before_first_save_then_transitioned_to_ready_on_a_second_save_persists_correctly()
    {
        var moduleId = SeedModuleId();
        var session = new LearningSession(moduleId, "S", "topic", "goal", 15, "Vocabulary", 0);
        session.MarkGenerationPending();
        _db.LearningSessions.Add(session);
        await _db.SaveChangesAsync();

        var afterPendingSave = await _db.LearningSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(GenerationStatus.Pending, afterPendingSave.GenerationStatus);

        session.MarkGenerationReady();
        await _db.SaveChangesAsync();

        var afterReadySave = await _db.LearningSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(GenerationStatus.Ready, afterReadySave.GenerationStatus);
        Assert.NotNull(afterReadySave.ReadyAtUtc);
    }
}
