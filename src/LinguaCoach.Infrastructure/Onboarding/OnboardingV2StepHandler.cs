using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingV2StepHandler : IOnboardingV2StepHandler
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingV2StepHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<SubmitOnboardingStepResult> HandleAsync(SubmitOnboardingStepCommand command, CancellationToken ct = default)
    {
        var progress = await _db.StudentOnboardingProgress
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new OnboardingV2ValidationException("No onboarding progress found. Call GET /api/onboarding first.");

        if (progress.IsComplete)
            throw new OnboardingV2ValidationException("Onboarding is already complete.");

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.Id == progress.FlowDefinitionId, ct)
            ?? throw new InvalidOperationException("Active onboarding flow not found.");

        var step = flow.Steps.FirstOrDefault(s => s.StepKey == command.StepKey)
            ?? throw new OnboardingV2ValidationException($"Step '{command.StepKey}' does not exist in the active flow.");

        if (!step.IsEnabled)
            throw new OnboardingV2ValidationException($"Step '{command.StepKey}' is not enabled.");

        // Validate answer against step rules.
        ValidateAnswer(step, command.AnswerJson);

        // Update StudentProfile preferences for system-required preference steps.
        await ApplyAnswerToProfileAsync(step, command.AnswerJson, command.UserId, ct);

        // Upsert response (unique constraint on progress_id + step_key).
        var existing = await _db.StudentOnboardingResponses
            .FirstOrDefaultAsync(r => r.ProgressId == progress.Id && r.StepKey == command.StepKey, ct);

        if (existing is null)
        {
            var response = new StudentOnboardingResponse(progress.Id, command.StepKey, command.AnswerJson);
            _db.StudentOnboardingResponses.Add(response);
        }
        else
        {
            // Re-submission replaces the answer (same student, same step).
            _db.StudentOnboardingResponses.Remove(existing);
            var response = new StudentOnboardingResponse(progress.Id, command.StepKey, command.AnswerJson);
            _db.StudentOnboardingResponses.Add(response);
        }

        progress.RecordStepCompleted(command.StepKey);

        // Advance to next enabled step.
        var orderedEnabledSteps = flow.Steps
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.StepOrder)
            .ToList();

        var currentIndex = orderedEnabledSteps.FindIndex(s => s.StepKey == command.StepKey);
        var nextStep = currentIndex >= 0 && currentIndex < orderedEnabledSteps.Count - 1
            ? orderedEnabledSteps[currentIndex + 1]
            : null;
        progress.UpdateCurrentStep(nextStep?.StepKey);

        // Percentage based on required+enabled steps only (rule 6).
        var requiredEnabledSteps = orderedEnabledSteps
            .Where(s => s.RequirementType == OnboardingStepRequirementType.SystemRequired)
            .ToList();

        var completedRequiredCount = requiredEnabledSteps
            .Count(s => progress.CompletedStepKeys.Contains(s.StepKey));

        var percentage = requiredEnabledSteps.Count > 0
            ? Math.Min(100, (int)Math.Round((double)completedRequiredCount / requiredEnabledSteps.Count * 100))
            : 100;

        progress.UpdatePercentage(percentage);

        await _db.SaveChangesAsync(ct);

        return new SubmitOnboardingStepResult(
            CurrentStepKey: progress.CurrentStepKey,
            CompletedStepKeys: progress.CompletedStepKeys,
            PercentageComplete: progress.PercentageComplete,
            IsComplete: progress.IsComplete
        );
    }

    private static void ValidateAnswer(OnboardingStepDefinition step, string answerJson)
    {
        // Parse validation metadata.
        int maxLength = 500;
        int maxSelections = 10;
        if (step.ValidationMetadataJson is not null)
        {
            var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(step.ValidationMetadataJson);
            if (meta is not null)
            {
                if (meta.TryGetValue("maxLength", out var ml) && ml.ValueKind == JsonValueKind.Number)
                    maxLength = ml.GetInt32();
                if (meta.TryGetValue("maxSelections", out var ms) && ms.ValueKind == JsonValueKind.Number)
                    maxSelections = ms.GetInt32();
            }
        }

        var parsed = JsonSerializer.Deserialize<JsonElement>(answerJson);

        switch (step.StepType)
        {
            case OnboardingStepTypeV2.FreeText or OnboardingStepTypeV2.PreferredName:
                var text = parsed.TryGetProperty("value", out var v) ? v.GetString() : null;
                if (text is not null && text.Length > maxLength)
                    throw new OnboardingV2ValidationException($"Answer exceeds maximum length of {maxLength} characters.");
                break;

            case OnboardingStepTypeV2.SingleChoice or OnboardingStepTypeV2.AssessmentQuestion:
                if (step.OptionsJson is not null)
                {
                    var validKeys = ParseOptionKeys(step.OptionsJson);
                    var selected = parsed.TryGetProperty("key", out var k) ? k.GetString() : null;
                    if (selected is not null && !validKeys.Contains(selected))
                        throw new OnboardingV2ValidationException($"Invalid option key '{selected}'.");
                }
                break;

            case OnboardingStepTypeV2.MultipleChoice or OnboardingStepTypeV2.LearningGoals
                or OnboardingStepTypeV2.FocusAreas:
                if (parsed.TryGetProperty("keys", out var keysEl) && keysEl.ValueKind == JsonValueKind.Array)
                {
                    var selectedKeys = keysEl.EnumerateArray().Select(e => e.GetString()).ToList();
                    if (selectedKeys.Count > maxSelections)
                        throw new OnboardingV2ValidationException($"Too many selections. Maximum is {maxSelections}.");

                    if (step.OptionsJson is not null)
                    {
                        var validKeys = ParseOptionKeys(step.OptionsJson);
                        var invalid = selectedKeys.Where(k => k is not null && !validKeys.Contains(k!)).ToList();
                        if (invalid.Any())
                            throw new OnboardingV2ValidationException($"Invalid option key(s): {string.Join(", ", invalid)}.");
                    }
                }
                break;
        }
    }

    private static HashSet<string> ParseOptionKeys(string optionsJson)
    {
        var options = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(optionsJson);
        return options?.Select(o => o["key"]).ToHashSet() ?? new HashSet<string>();
    }

    private async Task ApplyAnswerToProfileAsync(
        OnboardingStepDefinition step,
        string answerJson,
        Guid userId,
        CancellationToken ct)
    {
        if (step.AnswerMapping == OnboardingAnswerMapping.None) return;

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId, ct);
        if (profile is null) return;

        var parsed = JsonSerializer.Deserialize<JsonElement>(answerJson);

        switch (step.AnswerMapping)
        {
            case OnboardingAnswerMapping.PreferredName:
                var name = parsed.TryGetProperty("value", out var nv) ? nv.GetString() : null;
                profile.UpdateLearningPreferences(
                    preferredName: name,
                    supportLanguageCode: null, supportLanguageName: null, translationHelpPreference: null,
                    learningGoals: null, customLearningGoal: null, focusAreas: null,
                    customFocusArea: null, difficultyPreference: null, preferredSessionDurationMinutes: null);
                break;

            case OnboardingAnswerMapping.SupportLanguage:
                var code = parsed.TryGetProperty("languageCode", out var lc) ? lc.GetString() : null;
                var langName = parsed.TryGetProperty("languageName", out var ln) ? ln.GetString() : null;
                var helpPref = parsed.TryGetProperty("translationHelp", out var th)
                    ? Enum.TryParse<TranslationHelpPreference>(th.GetString(), out var tp) ? tp : (TranslationHelpPreference?)null
                    : null;
                profile.UpdateLearningPreferences(
                    preferredName: null, supportLanguageCode: code, supportLanguageName: langName,
                    translationHelpPreference: helpPref, learningGoals: null, customLearningGoal: null,
                    focusAreas: null, customFocusArea: null, difficultyPreference: null,
                    preferredSessionDurationMinutes: null);
                break;

            case OnboardingAnswerMapping.LearningGoals:
                var goals = ParseStringList(parsed, "keys");
                var customGoal = parsed.TryGetProperty("custom", out var cg) ? cg.GetString() : null;
                profile.UpdateLearningPreferences(
                    preferredName: null, supportLanguageCode: null, supportLanguageName: null,
                    translationHelpPreference: null, learningGoals: goals, customLearningGoal: customGoal,
                    focusAreas: null, customFocusArea: null, difficultyPreference: null,
                    preferredSessionDurationMinutes: null);
                break;

            case OnboardingAnswerMapping.FocusAreas:
                var areas = ParseStringList(parsed, "keys");
                var customArea = parsed.TryGetProperty("custom", out var ca) ? ca.GetString() : null;
                profile.UpdateLearningPreferences(
                    preferredName: null, supportLanguageCode: null, supportLanguageName: null,
                    translationHelpPreference: null, learningGoals: null, customLearningGoal: null,
                    focusAreas: areas, customFocusArea: customArea, difficultyPreference: null,
                    preferredSessionDurationMinutes: null);
                break;

            case OnboardingAnswerMapping.DifficultyPreference:
                var diffStr = parsed.TryGetProperty("key", out var dk) ? dk.GetString() : null;
                var diff = diffStr is not null && Enum.TryParse<DifficultyPreference>(diffStr, out var dp)
                    ? dp : (DifficultyPreference?)null;
                profile.UpdateLearningPreferences(
                    preferredName: null, supportLanguageCode: null, supportLanguageName: null,
                    translationHelpPreference: null, learningGoals: null, customLearningGoal: null,
                    focusAreas: null, customFocusArea: null, difficultyPreference: diff,
                    preferredSessionDurationMinutes: null);
                break;
        }
    }

    private static IReadOnlyList<string>? ParseStringList(JsonElement parsed, string propertyName)
    {
        if (parsed.TryGetProperty(propertyName, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
        return null;
    }
}
