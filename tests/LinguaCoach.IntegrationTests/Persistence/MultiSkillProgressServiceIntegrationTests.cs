using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for MultiSkillProgressService.
/// Verifies actual DB writes against SQLite in-memory.
/// </summary>
public sealed class MultiSkillProgressServiceIntegrationTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly MultiSkillProgressService _sut;
    private readonly Guid _profileId;

    public MultiSkillProgressServiceIntegrationTests()
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

    // ── Pattern with SecondarySkillsJson updates secondary skills ─────────────

    [Fact]
    public async Task PatternWithSecondarySkills_UpdatesAllSkillRows()
    {
        // Simulate a pattern evaluation: writing + grammar + vocabulary
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "writing",
            SecondarySkills: ["grammar", "vocabulary"],
            NormalizedScore: 85,
            Completed: true,
            Source: "pattern_evaluation");

        var result = await _sut.ApplyAsync(req);

        var skills = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == _profileId)
            .ToListAsync();

        Assert.Equal(3, skills.Count);
        Assert.Contains(skills, s => s.SkillKey == "writing");
        Assert.Contains(skills, s => s.SkillKey == "grammar");
        Assert.Contains(skills, s => s.SkillKey == "vocabulary");
        Assert.Equal(3, result.UpdatedSkills.Count);
    }

    // ── Single-skill activity still updates correctly ─────────────────────────

    [Fact]
    public async Task SingleSkillActivity_UpdatesOnlyPrimaryRow()
    {
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "vocabulary",
            SecondarySkills: [],
            NormalizedScore: 70,
            Completed: true,
            Source: "vocabulary_practice");

        await _sut.ApplyAsync(req);

        var count = await _db.StudentSkillProfiles.CountAsync(x => x.StudentProfileId == _profileId);
        Assert.Equal(1, count);
        var skill = await _db.StudentSkillProfiles.FirstAsync(x => x.StudentProfileId == _profileId);
        Assert.Equal("vocabulary", skill.SkillKey);
    }

    // ── Listening + writing updates both skills ───────────────────────────────

    [Fact]
    public async Task ListeningActivity_UpdatesListeningAndWriting()
    {
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "listening",
            SecondarySkills: ["writing"],
            NormalizedScore: 75,
            Completed: true,
            Source: "listening_comprehension");

        await _sut.ApplyAsync(req);

        var listening = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "listening");
        var writing = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "writing");

        Assert.NotNull(listening);
        Assert.NotNull(writing);
        // Primary (listening) score > secondary (writing) score due to 70/30 weighting
        Assert.True(listening.ScorePercent > writing.ScorePercent);
    }

    // ── Speaking roleplay updates speaking + fluency + pronunciation ──────────

    [Fact]
    public async Task SpeakingRoleplay_UpdatesSpeakingFluencyPronunciation()
    {
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "speaking",
            SecondarySkills: ["fluency", "pronunciation"],
            NormalizedScore: 90,
            Completed: true,
            Source: "speaking_roleplay");

        var result = await _sut.ApplyAsync(req);

        Assert.Contains("speaking", result.UpdatedSkills);
        Assert.Contains("fluency", result.UpdatedSkills);
        Assert.Contains("pronunciation", result.UpdatedSkills);

        var speaking = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "speaking");
        Assert.NotNull(speaking);
        Assert.False(speaking.IsWeak); // good score → not weak
    }

    // ── Writing activity updates writing + grammar + vocabulary ───────────────

    [Fact]
    public async Task WritingActivity_UpdatesWritingGrammarVocabulary()
    {
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "writing",
            SecondarySkills: ["grammar", "vocabulary"],
            NormalizedScore: 65,
            Completed: true,
            Source: "writing_scenario");

        await _sut.ApplyAsync(req);

        foreach (var key in new[] { "writing", "grammar", "vocabulary" })
        {
            var skill = await _db.StudentSkillProfiles
                .FirstOrDefaultAsync(x => x.StudentProfileId == _profileId && x.SkillKey == key);
            Assert.NotNull(skill);
        }
    }

    // ── Ledger result includes impacted skills ────────────────────────────────

    [Fact]
    public async Task Result_ScoreDeltaBySkill_ContainsAllUpdatedKeys()
    {
        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "reading",
            SecondarySkills: ["vocabulary", "grammar"],
            NormalizedScore: 80,
            Completed: true,
            Source: "reading_task");

        var result = await _sut.ApplyAsync(req);

        Assert.True(result.ScoreDeltaBySkill.ContainsKey("reading"));
        Assert.True(result.ScoreDeltaBySkill.ContainsKey("vocabulary"));
        Assert.True(result.ScoreDeltaBySkill.ContainsKey("grammar"));
    }

    // ── Existing profile row is updated, not duplicated ───────────────────────

    [Fact]
    public async Task ExistingProfileRow_Updated_NoDuplicate()
    {
        _db.StudentSkillProfiles.Add(new StudentSkillProfile(_profileId, "writing", "Writing", isWeak: true));
        await _db.SaveChangesAsync();

        var req = new MultiSkillProgressUpdateRequest(
            StudentProfileId: _profileId,
            PrimarySkill: "writing",
            SecondarySkills: [],
            NormalizedScore: 100,
            Completed: true,
            Source: "test");

        await _sut.ApplyAsync(req);

        var count = await _db.StudentSkillProfiles
            .CountAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "writing");
        Assert.Equal(1, count);
        var skill = await _db.StudentSkillProfiles
            .FirstAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "writing");
        // Started at 40 (weak), delta +10 from score 100 → 50
        Assert.Equal(50, skill.ScorePercent);
    }

    // ── BuildRequest: pattern metadata overrides ActivityType ─────────────────

    [Fact]
    public async Task BuildRequest_PatternMetadata_OverridesActivityTypeFallback()
    {
        // Pattern says "listening" primary + "writing" secondary, even for ActivityType.WritingScenario
        var req = _sut.BuildRequest(
            _profileId,
            exercisePatternKey: "listen_and_answer",
            patternPrimarySkill: "listening",
            patternSecondarySkills: ["writing"],
            activityType: ActivityType.WritingScenario, // ActivityType fallback would give writing
            normalizedScore: 80,
            completed: true,
            source: "test");

        Assert.Equal("listening", req.PrimarySkill);
        await _sut.ApplyAsync(req);

        Assert.NotNull(await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(x => x.StudentProfileId == _profileId && x.SkillKey == "listening"));
    }

    // ── No workplace default is introduced ───────────────────────────────────

    [Fact]
    public async Task NoPatternNoFallback_DoesNotDefaultToWorkplace()
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

        await _sut.ApplyAsync(req);

        var workplaceSkills = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == _profileId && x.SkillKey.StartsWith("workplace"))
            .CountAsync();
        Assert.Equal(0, workplaceSkills);
    }
}
