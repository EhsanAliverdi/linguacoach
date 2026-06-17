using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Unit tests for MultiSkillProgressService.
/// Uses SQLite in-memory so DB writes are tested without a full web stack.
/// </summary>
public sealed class MultiSkillProgressServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly MultiSkillProgressService _sut;
    private readonly Guid _profileId;

    public MultiSkillProgressServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new MultiSkillProgressService(_db, NullLogger<MultiSkillProgressService>.Instance);

        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        _profileId = student.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── Weighting rules ───────────────────────────────────────────────────────

    [Fact]
    public async Task PrimaryOnly_NoSecondary_Updates100Percent()
    {
        var req = MakeRequest("writing", [], 100, completed: true);
        await _sut.ApplyAsync(req);

        var profile = await GetSkill("writing");
        Assert.NotNull(profile);
        // score 100 → delta +1.0 full weight → +10 points on a 50 base
        Assert.True(profile.ScorePercent > StudentSkillProfile.DefaultScorePercent);
    }

    [Fact]
    public async Task PrimaryPlusOneSecondary_Splits70_30()
    {
        var req = MakeRequest("writing", ["grammar"], 100, completed: true);
        await _sut.ApplyAsync(req);

        var primary = await GetSkill("writing");
        var secondary = await GetSkill("grammar");

        Assert.NotNull(primary);
        Assert.NotNull(secondary);

        // Primary delta = 1.0 * 0.70 * 10 = 7; secondary = 1.0 * 0.30 * 10 = 3
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 7, primary.ScorePercent);
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 3, secondary.ScorePercent);
    }

    [Fact]
    public async Task PrimaryPlusTwoSecondary_Splits70_15_15()
    {
        var req = MakeRequest("writing", ["grammar", "vocabulary"], 100, completed: true);
        await _sut.ApplyAsync(req);

        var primary    = await GetSkill("writing");
        var secondary1 = await GetSkill("grammar");
        var secondary2 = await GetSkill("vocabulary");

        Assert.NotNull(primary);
        Assert.NotNull(secondary1);
        Assert.NotNull(secondary2);

        // primary: 1.0 * 0.70 * 10 = 7; each secondary: 1.0 * 0.15 * 10 = 1.5 → round to 2
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 7, primary.ScorePercent);
        // secondary delta = 1.0 * 0.30 / 2 * 10 = 1.5 → rounds to 2
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 2, secondary1.ScorePercent);
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 2, secondary2.ScorePercent);
    }

    [Fact]
    public async Task DuplicateSecondarySkill_DeduplicatedBeforeApply()
    {
        // "grammar" appears twice; should only create one row
        var req = MakeRequest("writing", ["grammar", "grammar"], 100, completed: true);
        await _sut.ApplyAsync(req);

        var count = await _db.StudentSkillProfiles
            .CountAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "grammar");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SecondaryEqualsToPrimary_DeduplicatedBeforeApply()
    {
        // secondary == primary; should only write one row for "writing"
        var req = MakeRequest("writing", ["writing"], 100, completed: true);
        await _sut.ApplyAsync(req);

        var count = await _db.StudentSkillProfiles
            .CountAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "writing");
        Assert.Equal(1, count);
        // And it should be treated as primary-only (full weight)
        var profile = await GetSkill("writing");
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 10, profile!.ScorePercent);
    }

    [Fact]
    public async Task UnknownPrimarySkill_WritesNothing()
    {
        var req = MakeRequest("totally_unknown_skill", [], 100, completed: true);
        await _sut.ApplyAsync(req);

        var count = await _db.StudentSkillProfiles.CountAsync(x => x.StudentProfileId == _profileId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UnknownSecondarySkill_DroppedSilently()
    {
        var req = MakeRequest("writing", ["totally_unknown"], 100, completed: true);
        await _sut.ApplyAsync(req);

        // writing should still be created; unknown secondary silently dropped
        var count = await _db.StudentSkillProfiles.CountAsync(x => x.StudentProfileId == _profileId);
        Assert.Equal(1, count);
        // Because secondary was dropped, primary gets 100% weight
        var profile = await GetSkill("writing");
        Assert.Equal(StudentSkillProfile.DefaultScorePercent + 10, profile!.ScorePercent);
    }

    [Fact]
    public async Task NotCompleted_SkipsUpdate()
    {
        var req = MakeRequest("writing", ["grammar"], 100, completed: false);
        var result = await _sut.ApplyAsync(req);

        Assert.Empty(result.UpdatedSkills);
        var count = await _db.StudentSkillProfiles.CountAsync(x => x.StudentProfileId == _profileId);
        Assert.Equal(0, count);
    }

    // ── ActivityType fallback ─────────────────────────────────────────────────

    [Fact]
    public void BuildRequest_PatternMetadataPresent_UsesPrimaryFromPattern()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: "email_reply",
            patternPrimarySkill: "writing",
            patternSecondarySkills: ["grammar"],
            activityType: ActivityType.WritingScenario,
            normalizedScore: 80,
            completed: true,
            source: "test");

        Assert.Equal("writing", req.PrimarySkill);
        Assert.Contains("grammar", req.SecondarySkills);
    }

    [Fact]
    public void BuildRequest_NoPatternMetadata_FallsBackToActivityType()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: ActivityType.ListeningComprehension,
            normalizedScore: 70,
            completed: true,
            source: "test");

        Assert.Equal("listening", req.PrimarySkill);
        Assert.Contains("writing", req.SecondarySkills);
    }

    [Fact]
    public void BuildRequest_WritingScenario_FallbackIncludesGrammarAndVocabulary()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: ActivityType.WritingScenario,
            normalizedScore: 80,
            completed: true,
            source: "test");

        Assert.Equal("writing", req.PrimarySkill);
        Assert.Contains("grammar", req.SecondarySkills);
        Assert.Contains("vocabulary", req.SecondarySkills);
    }

    [Fact]
    public void BuildRequest_SpeakingRolePlay_FallbackIncludesFluencyPronunciation()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: ActivityType.SpeakingRolePlay,
            normalizedScore: 80,
            completed: true,
            source: "test");

        Assert.Equal("speaking", req.PrimarySkill);
        Assert.Contains("fluency", req.SecondarySkills);
        Assert.Contains("pronunciation", req.SecondarySkills);
    }

    [Fact]
    public void BuildRequest_NoFallbackForType_EmptyPrimary()
    {
        // ActivityType with no entry in fallback map → empty primary → will be dropped safely
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: (ActivityType)999,
            normalizedScore: 80,
            completed: true,
            source: "test");

        Assert.Equal(string.Empty, req.PrimarySkill);
    }

    // ── Pattern metadata overrides ActivityType fallback ─────────────────────

    [Fact]
    public async Task ListeningActivity_UpdatesBothListeningAndWriting()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: ActivityType.ListeningComprehension,
            normalizedScore: 80,
            completed: true,
            source: "test");

        await _sut.ApplyAsync(req);

        var listening = await GetSkill("listening");
        var writing   = await GetSkill("writing");
        Assert.NotNull(listening);
        Assert.NotNull(writing);
    }

    [Fact]
    public async Task WritingActivity_UpdatesWritingGrammarVocabulary()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: ActivityType.WritingScenario,
            normalizedScore: 80,
            completed: true,
            source: "test");

        await _sut.ApplyAsync(req);

        Assert.NotNull(await GetSkill("writing"));
        Assert.NotNull(await GetSkill("grammar"));
        Assert.NotNull(await GetSkill("vocabulary"));
    }

    // ── Failed/incomplete attempt behaviour ───────────────────────────────────

    [Fact]
    public async Task FailedAttempt_NotCompleted_NoSkillUpdate()
    {
        var req = MakeRequest("writing", ["grammar"], 30, completed: false);
        var result = await _sut.ApplyAsync(req);

        Assert.Empty(result.UpdatedSkills);
        Assert.Contains("not completed", result.Notes, StringComparison.OrdinalIgnoreCase);
    }

    // ── Idempotency / repeated completions ───────────────────────────────────

    [Fact]
    public async Task RepeatedCompletion_UpdatesExistingProfile_DoesNotDuplicate()
    {
        var req = MakeRequest("writing", [], 80, completed: true);
        await _sut.ApplyAsync(req);
        await _sut.ApplyAsync(req);

        var count = await _db.StudentSkillProfiles
            .CountAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "writing");
        Assert.Equal(1, count); // no duplicate rows
    }

    // ── Ledger metadata fields ────────────────────────────────────────────────

    [Fact]
    public async Task Result_ContainsDeltaForEachUpdatedSkill()
    {
        var req = MakeRequest("writing", ["grammar"], 100, completed: true);
        var result = await _sut.ApplyAsync(req);

        Assert.Contains("writing", result.UpdatedSkills);
        Assert.Contains("grammar", result.UpdatedSkills);
        Assert.True(result.ScoreDeltaBySkill.ContainsKey("writing"));
        Assert.True(result.ScoreDeltaBySkill.ContainsKey("grammar"));
    }

    // ── Lower-level / review content ─────────────────────────────────────────

    [Fact]
    public async Task LowerLevelContent_StillUpdatesSkills_NotesContainLowerLevelFlag()
    {
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "writing",
            SecondarySkills: [],
            NormalizedScore: 90,
            Completed: true,
            Source: "test",
            IsLowerLevelContent: true,
            RoutingReason: "scaffold");

        var result = await _sut.ApplyAsync(req);

        Assert.Contains("writing", result.UpdatedSkills);
        Assert.Contains("lower-level", result.Notes, StringComparison.OrdinalIgnoreCase);
    }

    // ── No workplace default ──────────────────────────────────────────────────

    [Fact]
    public void BuildRequest_NoPatternOrActivityFallback_DoesNotDefaultToWorkplace()
    {
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: null,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: (ActivityType)999,
            normalizedScore: 80,
            completed: true,
            source: "test");

        Assert.NotEqual("workplace_vocabulary", req.PrimarySkill);
        Assert.DoesNotContain("workplace_vocabulary", req.SecondarySkills);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private MultiSkillProgressUpdateRequest MakeRequest(
        string primary,
        IReadOnlyList<string> secondary,
        double score,
        bool completed) =>
        new(
            StudentProfileId: _profileId,
            PrimarySkill: primary,
            SecondarySkills: secondary,
            NormalizedScore: score,
            Completed: completed,
            Source: "unit_test");

    private Task<StudentSkillProfile?> GetSkill(string key) =>
        _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _profileId && x.SkillKey == key);
}
