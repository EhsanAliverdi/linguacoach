using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Questions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingV2StepHandler : IOnboardingV2StepHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IQuestionAnswerValidator _validator;

    public OnboardingV2StepHandler(LinguaCoachDbContext db, IQuestionAnswerValidator validator)
    {
        _db = db;
        _validator = validator;
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

        // Unified Question-Schema Phase 6b: validate the shared QuestionAnswer shape against the
        // step's (dynamically-resolved) Content — replaces the old per-StepType switch. Info steps
        // (Welcome/Summary) have no Content, so nothing to validate.
        var content = await OnboardingContentResolver.ResolveAsync(step.Content, _db, ct);
        var answer = ParseAnswer(command.AnswerJson);

        if (content is not null && !IsSkip(step, answer))
        {
            var result = _validator.Validate(content, answer);
            if (!result.IsValid)
                throw new OnboardingV2ValidationException(result.Error ?? "Invalid answer.");
        }

        // Update StudentProfile preferences for system-required preference steps.
        await ApplyAnswerToProfileAsync(step, content, answer, command.UserId, ct);

        // Upsert response (unique constraint on progress_id + step_key).
        var existing = await _db.StudentOnboardingResponses
            .FirstOrDefaultAsync(r => r.ProgressId == progress.Id && r.StepKey == command.StepKey, ct);

        if (existing is not null)
            _db.StudentOnboardingResponses.Remove(existing);

        _db.StudentOnboardingResponses.Add(new StudentOnboardingResponse(progress.Id, command.StepKey, command.AnswerJson));

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

    private static QuestionAnswer ParseAnswer(string answerJson) =>
        QuestionContentJson.TryDeserializeAnswerOrEmpty(answerJson);

    /// <summary>AdminConfigured (skippable) steps submit an empty answers array to skip — valid
    /// regardless of the step's own validation rules (mirrors the old per-step-type "skip" buttons).</summary>
    private static bool IsSkip(OnboardingStepDefinition step, QuestionAnswer answer) =>
        step.RequirementType == OnboardingStepRequirementType.AdminConfigured && answer.Answers.Count == 0;

    private async Task ApplyAnswerToProfileAsync(
        OnboardingStepDefinition step,
        QuestionContent? content,
        QuestionAnswer answer,
        Guid userId,
        CancellationToken ct)
    {
        if (step.AnswerMapping == OnboardingAnswerMapping.None) return;

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId, ct);
        if (profile is null) return;

        var questionId = content?.Id ?? "q1";
        var values = answer.Find(questionId)?.Values ?? [];
        var single = values.FirstOrDefault();

        switch (step.AnswerMapping)
        {
            case OnboardingAnswerMapping.PreferredName:
                UpdatePreferencesPreservingOthers(profile, preferredName: single);
                break;

            case OnboardingAnswerMapping.SupportLanguage:
                var code = string.IsNullOrWhiteSpace(single) || single == "none" ? null : single;
                var name = code is null ? null : await _db.Languages.Where(l => l.Code == code).Select(l => l.Name).FirstOrDefaultAsync(ct);
                var help = code is null ? TranslationHelpPreference.Never : TranslationHelpPreference.WhenDifficult;
                UpdatePreferencesPreservingOthers(profile,
                    supportLanguageCode: code, supportLanguageName: name, translationHelpPreference: help,
                    forceSupportLanguage: true);
                break;

            case OnboardingAnswerMapping.LearningGoals:
                UpdatePreferencesPreservingOthers(profile, learningGoals: values);
                break;

            case OnboardingAnswerMapping.CustomLearningGoal:
                profile.UpdateLearningPreferences(
                    preferredName: profile.PreferredName, supportLanguageCode: profile.SupportLanguageCode,
                    supportLanguageName: profile.SupportLanguageName, translationHelpPreference: profile.TranslationHelpPreference,
                    learningGoals: profile.LearningGoals, customLearningGoal: string.IsNullOrWhiteSpace(single) ? null : single,
                    focusAreas: profile.FocusAreas, customFocusArea: profile.CustomFocusArea,
                    difficultyPreference: profile.DifficultyPreference, preferredSessionDurationMinutes: profile.PreferredSessionDurationMinutes);
                break;

            case OnboardingAnswerMapping.FocusAreas:
                UpdatePreferencesPreservingOthers(profile, focusAreas: values);
                break;

            case OnboardingAnswerMapping.CustomFocusArea:
                profile.UpdateLearningPreferences(
                    preferredName: profile.PreferredName, supportLanguageCode: profile.SupportLanguageCode,
                    supportLanguageName: profile.SupportLanguageName, translationHelpPreference: profile.TranslationHelpPreference,
                    learningGoals: profile.LearningGoals, customLearningGoal: profile.CustomLearningGoal,
                    focusAreas: profile.FocusAreas, customFocusArea: string.IsNullOrWhiteSpace(single) ? null : single,
                    difficultyPreference: profile.DifficultyPreference, preferredSessionDurationMinutes: profile.PreferredSessionDurationMinutes);
                break;

            case OnboardingAnswerMapping.DifficultyPreference:
                var diff = single is not null && Enum.TryParse<DifficultyPreference>(single, out var dp) ? dp : (DifficultyPreference?)null;
                UpdatePreferencesPreservingOthers(profile, difficultyPreference: diff);
                break;

            case OnboardingAnswerMapping.CareerContext:
                profile.UpdateOnboardingFreeTextContext(careerContextText: single, learningGoalDescription: null);
                break;

            case OnboardingAnswerMapping.LearningGoalDescription:
                profile.UpdateOnboardingFreeTextContext(careerContextText: null, learningGoalDescription: single);
                break;

            case OnboardingAnswerMapping.SessionDuration:
                if (single is not null && int.TryParse(single, out var minutes) && minutes > 0)
                    UpdatePreferencesPreservingOthers(profile, preferredSessionDurationMinutes: minutes);
                break;

            case OnboardingAnswerMapping.ProfessionalExperienceLevel:
                if (single is not null && Enum.TryParse<ProfessionalExperienceLevel>(single, out var expLevel))
                    profile.SetProfessionalExperienceLevel(expLevel);
                break;

            case OnboardingAnswerMapping.RoleFamiliarity:
                if (single is not null && Enum.TryParse<RoleFamiliarity>(single, out var familiarity))
                    profile.SetRoleFamiliarity(familiarity);
                break;
        }
    }

    /// <summary>
    /// StudentProfile.UpdateLearningPreferences overwrites every scalar field it's given
    /// unconditionally (only LearningGoals/FocusAreas/PreferredSessionDurationMinutes skip the
    /// update when null) — safe for /profile's single full-form submission, but wrong for V2's
    /// one-field-per-step submission model: without this, submitting e.g. difficulty_preference
    /// silently wiped out whatever support_language had just set, and vice versa. Reads the
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
        "career_context", "professional_experience_level", "role_familiarity"
    };

    private async Task<bool> IsWorkRelevantAsync(Guid progressId, CancellationToken ct)
    {
        var goalsResponse = await _db.StudentOnboardingResponses
            .Where(r => r.ProgressId == progressId && r.StepKey == "learning_goals")
            .Select(r => r.AnswerJson)
            .FirstOrDefaultAsync(ct);

        if (goalsResponse is null) return false;

        var answer = ParseAnswer(goalsResponse);
        var values = answer.Answers.FirstOrDefault()?.Values ?? [];
        return values.Any(v => string.Equals(v, "work", StringComparison.OrdinalIgnoreCase));
    }
}
