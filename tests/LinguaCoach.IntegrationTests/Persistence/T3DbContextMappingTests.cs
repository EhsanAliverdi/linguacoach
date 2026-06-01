using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Mapping smoke tests for T3 entities against an in-process SQLite database.
/// Validates schema creation, FK wiring, and round-trip persistence.
/// </summary>
public sealed class T3DbContextMappingTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public T3DbContextMappingTests()
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

    private StudentProfile SeedStudent()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        return student;
    }

    private (Guid LangPairId, Guid CareerProfileId) SeedLangPairAndCareer()
    {
        var pair = _db.LanguagePairs.First();
        var career = _db.CareerProfiles.First();
        return (pair.Id, career.Id);
    }

    private SpeakingScenario SeedScenario(Guid careerProfileId, Guid langPairId, string title = "Test Scenario")
    {
        var scenario = new SpeakingScenario(
            careerProfileId, langPairId, title, "Goal.", 6,
            "target phrase", "Rubric.", "B1");
        _db.SpeakingScenarios.Add(scenario);
        _db.SaveChanges();
        return scenario;
    }

    // ── AiPrompt token budget columns ─────────────────────────────────────────

    [Fact]
    public void AiPrompt_TokenBudgetColumns_PersistedAndLoaded()
    {
        var prompt = new AiPrompt("lesson.writing.v1", "You are a writing coach.", maxInputTokens: 800, maxOutputTokens: 600);
        _db.AiPrompts.Add(prompt);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.AiPrompts.Single(p => p.Key == "lesson.writing.v1");
        Assert.Equal(800, loaded.MaxInputTokens);
        Assert.Equal(600, loaded.MaxOutputTokens);
    }

    [Fact]
    public void AiPrompt_WithoutTokenBudget_NullColumnsAllowed()
    {
        var prompt = new AiPrompt("no.budget.v1", "content");
        _db.AiPrompts.Add(prompt);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.AiPrompts.Single(p => p.Key == "no.budget.v1");
        Assert.Null(loaded.MaxInputTokens);
        Assert.Null(loaded.MaxOutputTokens);
    }

    // ── VocabularyEntry ───────────────────────────────────────────────────────

    [Fact]
    public void VocabularyEntry_CanBeSavedAndLoaded()
    {
        var student = SeedStudent();
        var (langPairId, _) = SeedLangPairAndCareer();

        var entry = new VocabularyEntry(student.Id, langPairId, "submittal", "A document submitted for review.");
        _db.VocabularyEntries.Add(entry);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.VocabularyEntries.Single(e => e.Word == "submittal");
        Assert.Equal(VocabularyStatus.New, loaded.Status);
        Assert.Equal(2.5, loaded.EaseFactor);
        Assert.Equal(student.Id, loaded.StudentProfileId);
    }

    [Fact]
    public void VocabularyEntry_StatusTransitions_PersistedCorrectly()
    {
        var student = SeedStudent();
        var (langPairId, _) = SeedLangPairAndCareer();

        var entry = new VocabularyEntry(student.Id, langPairId, "approval", "Formal sign-off.");
        entry.RecordExposure();
        entry.RecordRecognition(correct: true);
        entry.RecordRecall(correct: true);
        entry.SetMasteryScore(0.9);

        _db.VocabularyEntries.Add(entry);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.VocabularyEntries.Single(e => e.Word == "approval");
        Assert.Equal(VocabularyStatus.Mastered, loaded.Status);
        Assert.Equal(0.9, loaded.MasteryScore);
        Assert.Equal(2, loaded.CorrectCount);
    }

    // ── CurriculumWordList ────────────────────────────────────────────────────

    [Fact]
    public void CurriculumWordList_CanBeSavedAndLoaded()
    {
        var (langPairId, careerProfileId) = SeedLangPairAndCareer();

        var word = new CurriculumWordList(
            careerProfileId, langPairId,
            "revision", "A new version of a document.",
            "Please issue revision B.", priority: 1, tags: "document-control,formal");

        _db.CurriculumWordLists.Add(word);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.CurriculumWordLists.Single(w => w.Word == "revision");
        Assert.Equal(1, loaded.Priority);
        Assert.Equal("document-control,formal", loaded.Tags);
        Assert.Equal(careerProfileId, loaded.CareerProfileId);
    }

    // ── UserLearningSummary ───────────────────────────────────────────────────

    [Fact]
    public void UserLearningSummary_CanBeSavedAndLoaded()
    {
        var student = SeedStudent();
        var summary = new UserLearningSummary(student.Id);
        summary.Update("overuses please", "email closings improved");

        _db.UserLearningSummaries.Add(summary);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.UserLearningSummaries.Single(s => s.StudentProfileId == student.Id);
        Assert.Equal("overuses please", loaded.RecentWeaknesses);
        Assert.Equal("email closings improved", loaded.RecentProgress);
    }

    [Fact]
    public void UserLearningSummary_UniquePerStudent_EnforcedByIndex()
    {
        var student = SeedStudent();
        _db.UserLearningSummaries.Add(new UserLearningSummary(student.Id));
        _db.SaveChanges();

        _db.UserLearningSummaries.Add(new UserLearningSummary(student.Id));
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    // ── SpeakingScenario ──────────────────────────────────────────────────────

    [Fact]
    public void SpeakingScenario_CanBeSavedAndLoaded()
    {
        var (langPairId, careerProfileId) = SeedLangPairAndCareer();
        var scenario = SeedScenario(careerProfileId, langPairId, "Requesting document status");

        _db.ChangeTracker.Clear();
        var loaded = _db.SpeakingScenarios.Single(s => s.Title == "Requesting document status");
        Assert.Equal(6, loaded.MaxTurns);
        Assert.Equal("B1", loaded.DifficultyLevel);
    }

    // ── SpeakingSession ───────────────────────────────────────────────────────

    [Fact]
    public void SpeakingSession_CanBeSavedAndLoaded()
    {
        var student = SeedStudent();
        var (langPairId, careerProfileId) = SeedLangPairAndCareer();
        var scenario = SeedScenario(careerProfileId, langPairId);

        var session = new SpeakingSession(student.Id, scenario.Id, "B1", "Document Controller", 4);
        session.Start();
        _db.SpeakingSessions.Add(session);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.SpeakingSessions.Single(s => s.StudentProfileId == student.Id);
        Assert.Equal(SpeakingSessionStatus.InProgress, loaded.Status);
        Assert.Equal(4, loaded.MaxTurns);
        Assert.Equal("B1", loaded.CefrLevel);
        Assert.NotNull(loaded.StartedAt);
    }

    [Fact]
    public void SpeakingSession_Complete_PersistsScoreAndSummary()
    {
        var student = SeedStudent();
        var (langPairId, careerProfileId) = SeedLangPairAndCareer();
        var scenario = SeedScenario(careerProfileId, langPairId);

        var session = new SpeakingSession(student.Id, scenario.Id, "B1", "DC", 4);
        session.Start();
        session.Complete(78.5, "Good formal register throughout.");
        _db.SpeakingSessions.Add(session);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.SpeakingSessions.Single(s => s.StudentProfileId == student.Id);
        Assert.Equal(SpeakingSessionStatus.Completed, loaded.Status);
        Assert.Equal(78.5, loaded.OverallScore);
        Assert.Equal("Good formal register throughout.", loaded.SessionSummary);
    }

    // ── SpeakingTurn ──────────────────────────────────────────────────────────

    [Fact]
    public void SpeakingTurn_CanBeSavedAndLoaded()
    {
        var student = SeedStudent();
        var (langPairId, careerProfileId) = SeedLangPairAndCareer();
        var scenario = SeedScenario(careerProfileId, langPairId);

        var session = new SpeakingSession(student.Id, scenario.Id, "B1", "Document Controller", 4);
        session.Start();
        _db.SpeakingSessions.Add(session);
        _db.SaveChanges();

        var turn = new SpeakingTurn(session.Id, 1, "How would you ask for an update?");
        turn.RecordResponse(
            userTranscript: "Could you please give me an update?",
            aiReply: "Good. Now try a more formal version.",
            feedbackJson: "{\"overallComment\":\"Good start\"}",
            mistakesJson: "[]",
            pronunciationScore: 80,
            grammarScore: 85,
            vocabularyScore: 70,
            fluencyScore: 75,
            turnSummary: "Used polite request.");
        _db.SpeakingTurns.Add(turn);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.SpeakingTurns.Single(t => t.SpeakingSessionId == session.Id);
        Assert.Equal(1, loaded.TurnNumber);
        Assert.Contains("update", loaded.UserTranscript);
        Assert.Equal(85, loaded.GrammarScore);
        Assert.Contains("Good start", loaded.FeedbackJson);
        Assert.Null(loaded.UserAudioUrl);
    }

    [Fact]
    public void SpeakingTurn_TurnNumberUnique_PerSession_EnforcedByIndex()
    {
        var student = SeedStudent();
        var (langPairId, careerProfileId) = SeedLangPairAndCareer();
        var scenario = SeedScenario(careerProfileId, langPairId);

        var session = new SpeakingSession(student.Id, scenario.Id, "B1", "DC", 6);
        session.Start();
        _db.SpeakingSessions.Add(session);
        _db.SaveChanges();

        _db.SpeakingTurns.Add(new SpeakingTurn(session.Id, 1, "Question A"));
        _db.SaveChanges();

        _db.SpeakingTurns.Add(new SpeakingTurn(session.Id, 1, "Question B"));
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }
}
