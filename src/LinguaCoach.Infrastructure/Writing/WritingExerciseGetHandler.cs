using System.Text.Json;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Writing;

public sealed class WritingExerciseGetHandler : IGetWritingExerciseHandler, IGetWritingScenariosHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanner _learningPlanner;

    public WritingExerciseGetHandler(LinguaCoachDbContext db, ILearningPlanner learningPlanner)
    {
        _db = db;
        _learningPlanner = learningPlanner;
    }

    // ── Scenario list ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WritingScenarioDto>> HandleAsync(
        GetWritingScenariosQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Writing exercise requires completed onboarding.");

        var scenarios = await _db.WritingScenarios
            .Where(s => s.IsActive)
            .OrderBy(s => s.Difficulty)
            .ThenBy(s => s.Title)
            .ToListAsync(ct);

        return scenarios.Select(MapToDto).ToList();
    }

    // ── Single exercise (learning section + vocab) ────────────────────────────

    public async Task<WritingExerciseDto> HandleAsync(
        GetWritingExerciseQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Writing exercise requires completed onboarding.");

        var scenario = await _db.WritingScenarios
            .FirstOrDefaultAsync(s => s.Id == query.ScenarioId && s.IsActive, ct)
            ?? throw new InvalidOperationException("Writing scenario not found.");

        var plan = await _learningPlanner.BuildLessonPlanAsync(profile.Id, ct);

        var allVocab = plan.TargetVocabulary
            .Concat(plan.ReviewVocabulary)
            .Concat(plan.ReinforcementVocabulary)
            .Select(v => v.Word)
            .Distinct()
            .ToArray();

        var sourceLangName = profile.LanguagePair?.SourceLanguage?.Name ?? "Persian";
        var instruction = BuildInstruction(scenario, sourceLangName);

        var targetPhrases = DeserializeStringArray(scenario.TargetPhrasesJson);
        var scenarioVocab = DeserializeStringArray(scenario.TargetVocabularyJson);

        var combinedVocab = scenarioVocab.Union(allVocab, StringComparer.OrdinalIgnoreCase).ToArray();

        return new WritingExerciseDto(
            scenario.Title,
            scenario.Situation,
            scenario.LearningGoal,
            instruction,
            targetPhrases,
            combinedVocab,
            scenario.ExampleText,
            scenario.CommonMistakeToAvoid);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WritingScenarioDto MapToDto(WritingScenario s) => new(
        s.Id,
        s.Title,
        s.Situation,
        s.LearningGoal,
        s.Difficulty,
        DeserializeStringArray(s.TargetPhrasesJson),
        DeserializeStringArray(s.TargetVocabularyJson));

    private static string BuildInstruction(WritingScenario scenario, string sourceLangName)
    {
        // Generic Persian instruction — all MVP students are Persian speakers.
        // When multi-language support lands, look up source-language instruction templates.
        return sourceLangName.Equals("Persian", StringComparison.OrdinalIgnoreCase)
            ? $"لطفاً یک ایمیل حرفه‌ای و مودبانه بنویسید. هدف: {scenario.LearningGoal}"
            : $"Please write a professional email. Goal: {scenario.LearningGoal}";
    }

    private static string[] DeserializeStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
