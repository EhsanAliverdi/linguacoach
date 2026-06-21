using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace LinguaCoach.IntegrationTests.Notifications;

/// <summary>
/// Integration tests for email dispatch path.
/// Uses SQLite in-memory + a fake IEmailSender — no real SMTP calls.
/// </summary>
public sealed class NotificationEmailDispatchTests : IAsyncLifetime
{
    private SqliteConnection? _connection;
    private LinguaCoachDbContext? _db;
    private UserManager<ApplicationUser>? _userManager;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new LinguaCoachDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        // Set up a minimal UserManager backed by the same SQLite DB.
        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, LinguaCoachDbContext, Guid>(_db);
        _userManager = new UserManager<ApplicationUser>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    public async Task DisposeAsync()
    {
        _userManager?.Dispose();
        if (_db is not null) await _db.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private LinguaCoachDbContext Db => _db!;
    private UserManager<ApplicationUser> UserMgr => _userManager!;

    private INotificationService NotifSvc => new NotificationService(
        Db, NullLogger<NotificationService>.Instance);

    private INotificationDispatchService DispatchSvc(IEmailSender emailSender) =>
        new NotificationDispatchService(Db, emailSender, UserMgr, NullLogger<NotificationDispatchService>.Instance);

    private async Task<ApplicationUser> CreateUserAsync(string email = "student@example.com")
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Role = UserRole.Student,
        };
        var result = await UserMgr.CreateAsync(user, "P@ssw0rd!");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    // ── Fake sender — succeeds ────────────────────────────────────────────────

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();
        public EmailSendResult ResultToReturn { get; set; } = EmailSendResult.Ok();

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(ResultToReturn);
        }
    }

    // ── Disabled sender — always skips ───────────────────────────────────────

    private static IEmailSender DisabledSender() =>
        new DisabledEmailSender(NullLogger<DisabledEmailSender>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_Email_WithFakeSender_MarksDelivered()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "Password Reset", "Please log in.",
            NotificationCategory.Account, NotificationSeverity.Info);

        var fake = new FakeEmailSender();
        var result = await DispatchSvc(fake).DispatchDueAsync();

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);

        Assert.Single(fake.Sent);
        Assert.Equal(user.Email, fake.Sent[0].ToAddress);
        Assert.Contains("Password Reset", fake.Sent[0].Subject);
    }

    [Fact]
    public async Task Dispatch_Email_MarksOutboxDelivered()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        await DispatchSvc(new FakeEmailSender()).DispatchDueAsync();

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationStatus.Delivered, outbox.Status);
        Assert.NotNull(outbox.ProcessedAtUtc);
    }

    [Fact]
    public async Task Dispatch_Email_MarksNotificationDelivered()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        await DispatchSvc(new FakeEmailSender()).DispatchDueAsync();

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Delivered, notif.Status);
    }

    [Fact]
    public async Task Dispatch_Email_WithDisabledSender_CountsAsSkipped()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        var result = await DispatchSvc(DisabledSender()).DispatchDueAsync();

        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Dispatch_Email_WithDisabledSender_IncrementsAttemptCount()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        await DispatchSvc(DisabledSender()).DispatchDueAsync();

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(1, outbox.AttemptCount);
        Assert.NotNull(outbox.LastError);
    }

    [Fact]
    public async Task Dispatch_Email_WithFailingSender_CountsAsFailed()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        var fake = new FakeEmailSender { ResultToReturn = EmailSendResult.Failure("SMTP timeout") };
        var result = await DispatchSvc(fake).DispatchDueAsync();

        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Contains("SMTP timeout", outbox.LastError ?? "");
    }

    [Fact]
    public async Task Dispatch_Email_PasswordNotStoredInNotificationMetadata()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueEmailAsync(user.Id, "Your password has been reset",
            "Please log in with your new credentials.",
            NotificationCategory.Account, NotificationSeverity.Info);

        var notif = await Db.Notifications.SingleAsync();

        // MetadataJson must not contain any password-like sensitive content.
        Assert.Null(notif.MetadataJson);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.DoesNotContain("password=", outbox.PayloadJson ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("newPassword", outbox.PayloadJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_Sms_StillSkipped()
    {
        var user = await CreateUserAsync();
        await NotifSvc.QueueSmsAsync(user.Id, "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        var result = await DispatchSvc(new FakeEmailSender()).DispatchDueAsync();

        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Skipped);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
    }
}

/// <summary>
/// Unit tests for NotificationDispatchJob — verifies it invokes INotificationDispatchService.
/// </summary>
public sealed class NotificationDispatchJobTests
{
    private sealed class FakeDispatchService : INotificationDispatchService
    {
        public int CallCount { get; private set; }
        public int LastBatchSize { get; private set; }

        public Task<DispatchResult> DispatchDueAsync(int batchSize = 50, CancellationToken ct = default)
        {
            CallCount++;
            LastBatchSize = batchSize;
            return Task.FromResult(new DispatchResult(1, 0, 0));
        }
    }

    private sealed class FakeJobContext : IJobExecutionContext
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public IScheduler Scheduler => null!;
        public ITrigger Trigger => null!;
        public IJobDetail JobDetail => null!;
        public IJob JobInstance => null!;
        public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledFireTimeUtc => null;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public JobDataMap MergedJobDataMap => new();
        public JobDataMap JobDataMap => new();
        public string FireInstanceId => "test";
        public int RefireCount => 0;
        public TimeSpan JobRunTime => TimeSpan.Zero;
        public object? Result { get; set; }
        public bool Recovering => false;
        public TriggerKey RecoveringTriggerKey => null!;
        public ICalendar? Calendar => null;
        public void Put(object key, object objectValue) { }
        public object? Get(object key) => null;
    }

    [Fact]
    public async Task Job_InvokesDispatchService()
    {
        var fake = new FakeDispatchService();
        var job = new LinguaCoach.Infrastructure.Jobs.NotificationDispatchJob(
            fake,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LinguaCoach.Infrastructure.Jobs.NotificationDispatchJob>.Instance);

        await job.Execute(new FakeJobContext());

        Assert.Equal(1, fake.CallCount);
        Assert.Equal(50, fake.LastBatchSize);
    }

    [Fact]
    public async Task Job_ThrowsJobExecutionException_WhenServiceFails()
    {
        var broken = new BrokenDispatchService();
        var job = new LinguaCoach.Infrastructure.Jobs.NotificationDispatchJob(
            broken,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LinguaCoach.Infrastructure.Jobs.NotificationDispatchJob>.Instance);

        await Assert.ThrowsAsync<JobExecutionException>(() => job.Execute(new FakeJobContext()));
    }

    private sealed class BrokenDispatchService : INotificationDispatchService
    {
        public Task<DispatchResult> DispatchDueAsync(int batchSize = 50, CancellationToken ct = default) =>
            throw new InvalidOperationException("DB offline");
    }

}
