using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace LinguaCoach.UnitTests.Admin;

/// <summary>
/// Unit tests for GenerationValidationFailurePruneJob using SQLite in-memory DbContext.
/// </summary>
public sealed class GenerationValidationFailurePruneJobTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public GenerationValidationFailurePruneJobTests()
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

    private GenerationValidationFailurePruneJob BuildJob(int retentionDays = 90)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GenerationQuality:RetentionDays"] = retentionDays.ToString(),
            })
            .Build();
        return new GenerationValidationFailurePruneJob(_db, config, NullLogger<GenerationValidationFailurePruneJob>.Instance);
    }

    private static IJobExecutionContext MakeFakeContext() =>
        new FakeJobExecutionContext();

    [Fact]
    public async Task Retention_DeletesOldRows()
    {
        var entity = new GenerationValidationFailure("WritingScenario", "Old", 1);
        _db.GenerationValidationFailures.Add(entity);
        await _db.SaveChangesAsync();
        await BackdateCreatedAtAsync(entity.Id, DateTime.UtcNow.AddDays(-100));

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var old = await _db.GenerationValidationFailures.Where(f => f.CreatedAt < cutoff).ToListAsync();
        _db.GenerationValidationFailures.RemoveRange(old);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.GenerationValidationFailures.CountAsync());
    }

    [Fact]
    public async Task Retention_DoesNotDeleteRecentRows()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure("WritingScenario", "Recent", 1));
        await _db.SaveChangesAsync();

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var old = await _db.GenerationValidationFailures.Where(f => f.CreatedAt < cutoff).ToListAsync();
        _db.GenerationValidationFailures.RemoveRange(old);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.GenerationValidationFailures.CountAsync());
    }

    [Fact]
    public async Task Retention_OnlyDeletesOlderThanRetentionWindow()
    {
        var oldEntity = new GenerationValidationFailure("WritingScenario", "Old", 1);
        var recentEntity = new GenerationValidationFailure("WritingScenario", "Recent", 1);
        _db.GenerationValidationFailures.AddRange(oldEntity, recentEntity);
        await _db.SaveChangesAsync();
        await BackdateCreatedAtAsync(oldEntity.Id, DateTime.UtcNow.AddDays(-100));

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var old = await _db.GenerationValidationFailures.Where(f => f.CreatedAt < cutoff).ToListAsync();
        _db.GenerationValidationFailures.RemoveRange(old);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.GenerationValidationFailures.CountAsync());
        Assert.Equal(recentEntity.Id, (await _db.GenerationValidationFailures.SingleAsync()).Id);
    }

    [Fact]
    public async Task Execute_DoesNotDeleteRecentRows()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure("WritingScenario", "Recent error", 1));
        await _db.SaveChangesAsync();

        var job = BuildJob(retentionDays: 90);
        await job.Execute(MakeFakeContext());

        Assert.Equal(1, await _db.GenerationValidationFailures.CountAsync());
    }

    private async Task BackdateCreatedAtAsync(Guid id, DateTime timestamp)
    {
        // Use ExecuteSqlAsync so EF Core serializes both the DateTime and Guid
        // using the same type mappings it uses for WHERE clause comparisons.
        await _db.Database.ExecuteSqlAsync(
            $"UPDATE generation_validation_failures SET created_at = {timestamp} WHERE id = {id}");
        _db.ChangeTracker.Clear();
    }

    /// <summary>Minimal fake for IJobExecutionContext (only CancellationToken needed).</summary>
    private sealed class FakeJobExecutionContext : IJobExecutionContext
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public IScheduler Scheduler => null!;
        public ITrigger Trigger => null!;
        public ICalendar? Calendar => null;
        public bool Recovering => false;
        public TriggerKey? RecoveringTriggerKey => null;
        public int RefireCount => 0;
        public JobDataMap MergedJobDataMap => new();
        public IJobDetail JobDetail => null!;
        public IJob JobInstance => null!;
        public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledFireTimeUtc => null;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public string FireInstanceId => Guid.NewGuid().ToString();
        public object? Result { get; set; }
        public TimeSpan JobRunTime => TimeSpan.Zero;
        public void Put(object key, object? objectValue) { }
        public object? Get(object key) => null;
    }
}
