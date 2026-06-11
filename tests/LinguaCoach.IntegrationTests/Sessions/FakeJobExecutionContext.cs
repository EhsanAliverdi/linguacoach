using Quartz;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// Minimal IJobExecutionContext test double. Only the members used by the jobs under test
/// (CancellationToken, Scheduler, MergedJobDataMap, FireInstanceId) are implemented.
/// </summary>
internal sealed class FakeJobExecutionContext : IJobExecutionContext
{
    public FakeJobExecutionContext(IScheduler scheduler, JobDataMap? data = null)
    {
        Scheduler = scheduler;
        MergedJobDataMap = data ?? new JobDataMap();
    }

    public IScheduler Scheduler { get; }
    public CancellationToken CancellationToken => CancellationToken.None;
    public JobDataMap MergedJobDataMap { get; }
    public string FireInstanceId => Guid.NewGuid().ToString("N");

    // Unused members.
    public ITrigger Trigger => throw new NotImplementedException();
    public ICalendar? Calendar => null;
    public bool Recovering => false;
    public TriggerKey RecoveringTriggerKey => throw new NotImplementedException();
    public int RefireCount => 0;
    public IJobDetail JobDetail => throw new NotImplementedException();
    public IJob JobInstance => throw new NotImplementedException();
    public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledFireTimeUtc => null;
    public DateTimeOffset? PreviousFireTimeUtc => null;
    public DateTimeOffset? NextFireTimeUtc => null;
    public TimeSpan JobRunTime => TimeSpan.Zero;
    public object? Result { get; set; }
    public object Get(object key) => throw new NotImplementedException();
    public void Put(object key, object objectValue) { }
}
