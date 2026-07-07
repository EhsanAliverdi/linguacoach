using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Uses SQLite in-memory so DB writes/queries are tested without a full web stack.
/// </summary>
public sealed class ActivityNoveltyPolicyTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityNoveltyPolicy _sut;
    private readonly Guid _studentId;
    private static readonly DateTime Now = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

    public ActivityNoveltyPolicyTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var settings = Options.Create(new NoveltyPolicySettings
        {
            FingerprintCooldownDays = 60,
            TemplateCooldownDays = 3,
            TopicCooldownDays = 7,
            ScenarioCooldownDays = 7,
        });
        _sut = new ActivityNoveltyPolicy(_db, settings);

        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        _studentId = student.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Guid SeedTemplate()
    {
        var template = new ActivityTemplate("tmpl_" + Guid.NewGuid().ToString("N")[..8], "writing", "B1", "WritingScenario");
        _db.ActivityTemplates.Add(template);
        _db.SaveChanges();
        return template.Id;
    }

    private void SeedUsageLog(
        string fingerprint,
        DateTime consumedAtUtc,
        Guid? sourceTemplateId = null,
        string? topicKey = null,
        string? scenarioKey = null)
    {
        var log = new StudentActivityUsageLog(
            studentProfileId: _studentId,
            contentFingerprint: fingerprint,
            consumedAtUtc: consumedAtUtc,
            sourceTemplateId: sourceTemplateId,
            topicKey: topicKey,
            scenarioKey: scenarioKey);
        _db.StudentActivityUsageLogs.Add(log);
        _db.SaveChanges();
    }

    [Fact]
    public async Task SameFingerprint_WithinCooldown_IsBlocked()
    {
        SeedUsageLog("fp-1", Now.AddDays(-10));

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-1", NowUtc: Now));

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(NoveltyBlockReason.SameFingerprintTooRecent);
        result.MatchedFingerprint.Should().Be("fp-1");
        result.CooldownUntilUtc.Should().Be(Now.AddDays(-10).AddDays(60));
    }

    [Fact]
    public async Task SameFingerprint_OutsideCooldown_IsAllowed()
    {
        SeedUsageLog("fp-2", Now.AddDays(-61));

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-2", NowUtc: Now));

        result.Allowed.Should().BeTrue();
        result.Reason.Should().Be(NoveltyBlockReason.None);
    }

    [Fact]
    public async Task SameTemplate_WithinCooldown_IsBlocked()
    {
        var templateId = SeedTemplate();
        SeedUsageLog("fp-3", Now.AddDays(-1), sourceTemplateId: templateId);

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-different", SourceTemplateId: templateId, NowUtc: Now));

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(NoveltyBlockReason.SameTemplateTooRecent);
        result.MatchedTemplateId.Should().Be(templateId);
    }

    [Fact]
    public async Task SameTemplate_OutsideCooldown_IsAllowed()
    {
        var templateId = SeedTemplate();
        SeedUsageLog("fp-4", Now.AddDays(-4), sourceTemplateId: templateId);

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-different-2", SourceTemplateId: templateId, NowUtc: Now));

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task SameTopicKey_WithinCooldown_IsBlocked()
    {
        SeedUsageLog("fp-5", Now.AddDays(-2), topicKey: "vendor-delay");

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-different-3", TopicKey: "vendor-delay", NowUtc: Now));

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(NoveltyBlockReason.SameTopicTooRecent);
    }

    [Fact]
    public async Task SameScenarioKey_WithinCooldown_IsBlocked()
    {
        SeedUsageLog("fp-6", Now.AddDays(-2), scenarioKey: "late-shipment");

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-different-4", ScenarioKey: "late-shipment", NowUtc: Now));

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(NoveltyBlockReason.SameScenarioTooRecent);
    }

    [Fact]
    public async Task IntentionalReview_BypassesAllCooldowns()
    {
        var templateId = SeedTemplate();
        SeedUsageLog("fp-7", Now.AddDays(-1), sourceTemplateId: templateId, topicKey: "vendor-delay");

        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-7", SourceTemplateId: templateId, TopicKey: "vendor-delay",
            IsIntentionalReview: true, NowUtc: Now));

        result.Allowed.Should().BeTrue();
        result.Reason.Should().Be(NoveltyBlockReason.None);
    }

    [Fact]
    public async Task NoMatchingHistory_IsAllowed()
    {
        var result = await _sut.CheckAsync(new ActivityNoveltyCheckRequest(
            _studentId, "fp-never-seen", NowUtc: Now));

        result.Allowed.Should().BeTrue();
    }
}
