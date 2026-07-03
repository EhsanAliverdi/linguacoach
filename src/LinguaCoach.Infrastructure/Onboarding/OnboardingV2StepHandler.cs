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
        var candidateIndex = currentIndex + 1;
        OnboardingStepDefinition? nextStep = null;
        if (currentIndex >= 0)
        {
            var workRelevant = await IsWorkRelevantAsync(progress.Id, ct);
            while (candidateIndex < orderedEnabledSteps.Count)
            {
                var candidate = orderedEnabledSteps[candidateIndex];
                if (!workRelevant && WorkOnlyStepKeys.Contains(candidate.StepKey))
                {
                    candidateIndex++;
                    continue;
                }
                nextStep = candidate;
                break;
            }
        }
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

            case OnboardingStepTypeV2.SingleChoice or OnboardingStepTypeV2.AssessmentQuestion
                or OnboardingStepTypeV2.SessionDuration:
                if (step.OptionsJson is not null)
                {
                    var validKeys = ParseOptionKeys(step.OptionsJson);
                    var selected = parsed.TryGetProperty("key", out var k) ? k.GetString() : null;
                    if (selected is not null && !validKeys.Contains(selected))
                        throw new OnboardingV2ValidationException($"Invalid option key '{selected}'.");
                }
                break;

            case OnboardingStepTypeV2.WorkExperience:
                // This step is AdminConfigured (skippable) — an empty object represents
                // "skipped," which is valid. If either field is provided, both must be.
                var hasExperience = parsed.TryGetProperty("experienceLevel", out var expEl) && expEl.ValueKind == JsonValueKind.String;
                var hasFamiliarity = parsed.TryGetProperty("roleFamiliarity", out var famEl) && famEl.ValueKind == JsonValueKind.String;
                if (hasExperience != hasFamiliarity)
                    throw new OnboardingV2ValidationException("Both experienceLevel and roleFamiliarity are required together.");
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
                UpdatePreferencesPreservingOthers(profile, preferredName: name);
                break;

            case OnboardingAnswerMapping.SupportLanguage:
                var code = parsed.TryGetProperty("languageCode", out var lc) ? lc.GetString() : null;
                var langName = parsed.TryGetProperty("languageName", out var ln) ? ln.GetString() : null;
                var helpPref = parsed.TryGetProperty("translationHelp", out var th)
                    ? Enum.TryParse<TranslationHelpPreference>(th.GetString(), out var tp) ? tp : (TranslationHelpPreference?)null
                    : null;
                UpdatePreferencesPreservingOthers(profile,
                    supportLanguageCode: code, supportLanguageName: langName, translationHelpPreference: helpPref,
                    // "None"/cleared selections must actually clear these fields, not be treated
                    // as "untouched" — force-apply even when null since this step explicitly ran.
                    forceSupportLanguage: true);
                break;

            case OnboardingAnswerMapping.LearningGoals:
                var goals = ParseStringList(parsed, "keys");
                var customGoal = parsed.TryGetProperty("custom", out var cg) ? cg.GetString() : null;
                UpdatePreferencesPreservingOthers(profile, learningGoals: goals, customLearningGoal: customGoal);
                break;

            case OnboardingAnswerMapping.FocusAreas:
                var areas = ParseStringList(parsed, "keys");
                var customArea = parsed.TryGetProperty("custom", out var ca) ? ca.GetString() : null;
                UpdatePreferencesPreservingOthers(profile, focusAreas: areas, customFocusArea: customArea);
                break;

            case OnboardingAnswerMapping.DifficultyPreference:
                var diffStr = parsed.TryGetProperty("key", out var dk) ? dk.GetString() : null;
                var diff = diffStr is not null && Enum.TryParse<DifficultyPreference>(diffStr, out var dp)
                    ? dp : (DifficultyPreference?)null;
                UpdatePreferencesPreservingOthers(profile, difficultyPreference: diff);
                break;

            case OnboardingAnswerMapping.CareerContext:
                var careerText = parsed.TryGetProperty("value", out var cv) ? cv.GetString() : null;
                profile.UpdateOnboardingFreeTextContext(careerContextText: careerText, learningGoalDescription: null);
                break;

            case OnboardingAnswerMapping.LearningGoalDescription:
                var goalDescription = parsed.TryGetProperty("value", out var gv) ? gv.GetString() : null;
                profile.UpdateOnboardingFreeTextContext(careerContextText: null, learningGoalDescription: goalDescription);
                break;

            case OnboardingAnswerMapping.SessionDuration:
                var minutesStr = parsed.TryGetProperty("key", out var mk) ? mk.GetString() : null;
                if (minutesStr is not null && int.TryParse(minutesStr, out var minutes) && minutes > 0)
                    UpdatePreferencesPreservingOthers(profile, preferredSessionDurationMinutes: minutes);
                break;

            case OnboardingAnswerMapping.WorkExperience:
                var expStr = parsed.TryGetProperty("experienceLevel", out var el) ? el.GetString() : null;
                var famStr = parsed.TryGetProperty("roleFamiliarity", out var rf) ? rf.GetString() : null;
                if (expStr is not null && famStr is not null
                    && Enum.TryParse<ProfessionalExperienceLevel>(expStr, out var expLevel)
                    && Enum.TryParse<RoleFamiliarity>(famStr, out var familiarity))
                {
                    profile.SetExperienceContext(expLevel, familiarity);
                }
                break;
        }
    }

    /// <summary>
    /// StudentProfile.UpdateLearningPreferences overwrites every scalar field it's given
    /// unconditionally (only LearningGoals/FocusAreas/PreferredSessionDurationMinutes skip the
    /// update when null) — safe for /profile's single full-form submission, but wrong for V2's
    /// one-field-per-step submission model: without this, submitting e.g. difficulty_preference
    /// silently wiped out whatever support_language had just set, and vice versa (found live
    /// 2026-07-03: support_language_code and difficulty_preference were always empty after a
    /// full onboarding run, even though each step's own submission succeeded). Reads the
    /// profile's current values for every field the caller doesn't explicitly pass, so a step
    /// can only ever change the field(s) it owns.
    /// </summary>
    private static void UpdatePreferencesPreservingOthers(
        Domain.Entities.StudentProfile profile,
        string? preferredName = null,
        string? supportLanguageCode = null,
        string? supportLanguageName = null,
        TranslationHelpPreference? translationHelpPreference = null,
        IReadOnlyList<string>? learningGoals = null,
        string? customLearningGoal = null,
        IReadOnlyList<string>? focusAreas = null,
        string? customFocusArea = null,
        DifficultyPreference? difficultyPreference = null,
        int? preferredSessionDurationMinutes = null,
        bool forceSupportLanguage = false)
    {
        profile.UpdateLearningPreferences(
            preferredName: preferredName ?? profile.PreferredName,
            supportLanguageCode: forceSupportLanguage ? supportLanguageCode : supportLanguageCode ?? profile.SupportLanguageCode,
            supportLanguageName: forceSupportLanguage ? supportLanguageName : supportLanguageName ?? profile.SupportLanguageName,
            translationHelpPreference: forceSupportLanguage ? translationHelpPreference : translationHelpPreference ?? profile.TranslationHelpPreference,
            learningGoals: learningGoals ?? profile.LearningGoals,
            customLearningGoal: customLearningGoal ?? profile.CustomLearningGoal,
            focusAreas: focusAreas ?? profile.FocusAreas,
            customFocusArea: customFocusArea ?? profile.CustomFocusArea,
            difficultyPreference: difficultyPreference ?? profile.DifficultyPreference,
            preferredSessionDurationMinutes: preferredSessionDurationMinutes ?? profile.PreferredSessionDurationMinutes);
    }

    /// <summary>
    /// Career context and work-experience steps are only relevant to students whose learning
    /// goals include work/professional communication — everyone else skips straight past them.
    /// </summary>
    private static readonly HashSet<string> WorkOnlyStepKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "career_context", "work_experience"
    };

    private async Task<bool> IsWorkRelevantAsync(Guid progressId, CancellationToken ct)
    {
        var goalsResponse = await _db.StudentOnboardingResponses
            .Where(r => r.ProgressId == progressId && r.StepKey == "learning_goals")
            .Select(r => r.AnswerJson)
            .FirstOrDefaultAsync(ct);

        if (goalsResponse is null) return false;

        var parsed = JsonSerializer.Deserialize<JsonElement>(goalsResponse);
        if (!parsed.TryGetProperty("keys", out var keysEl) || keysEl.ValueKind != JsonValueKind.Array)
            return false;

        return keysEl.EnumerateArray()
            .Any(k => string.Equals(k.GetString(), "work", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string>? ParseStringList(JsonElement parsed, string propertyName)
    {
        if (parsed.TryGetProperty(propertyName, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
        return null;
    }
}
