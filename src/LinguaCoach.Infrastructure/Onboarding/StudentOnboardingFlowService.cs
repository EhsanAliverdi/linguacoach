using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Services;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

/// <summary>Student-facing onboarding flow driven by the published Form.io
/// StudentFlowTemplateVersion. Normalizes a one-shot Form.io submission into StudentProfile
/// updates using the same component-key convention as the legacy OnboardingAnswerMapping enum,
/// scores the ten CEFR quick-check questions (assessment_q1..assessment_q10) against the
/// template's backend-only ScoringRulesJson, and advances StudentLifecycleStage exactly as the
/// old V2 completion handler did.</summary>
public sealed class StudentOnboardingFlowService :
    IStudentOnboardingActiveQuery,
    IStudentOnboardingSaveDraftHandler,
    IStudentOnboardingSubmitHandler
{
    private readonly LinguaCoachDbContext _db;

    private static readonly string[] QuickCheckKeys =
        Enumerable.Range(1, 10).Select(n => $"assessment_q{n}").ToArray();

    public StudentOnboardingFlowService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<StudentOnboardingActiveDto> HandleAsync(GetStudentOnboardingActiveQuery query, CancellationToken ct = default)
    {
        var version = await GetActivePublishedVersionAsync(ct)
            ?? throw new InvalidOperationException("No published onboarding template is available.");

        var existing = await _db.StudentFlowSubmissions
            .Where(s => s.StudentId == query.UserId && s.FlowKind == StudentFlowKind.Onboarding)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var isComplete = existing?.Status == StudentFlowSubmissionStatus.Evaluated;

        return new StudentOnboardingActiveDto(
            TemplateVersionId: version.Id,
            FormIoSchemaJson: version.FormIoSchemaJson,
            RendererKind: version.RendererKind.ToString(),
            SubmissionJson: isComplete ? null : existing?.SubmissionJson,
            IsComplete: isComplete);
    }

    public async Task HandleAsync(SaveOnboardingDraftCommand command, CancellationToken ct = default)
    {
        var version = await GetActivePublishedVersionAsync(ct)
            ?? throw new InvalidOperationException("No published onboarding template is available.");

        var submission = await _db.StudentFlowSubmissions
            .Where(s => s.StudentId == command.UserId && s.FlowKind == StudentFlowKind.Onboarding)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (submission is null || submission.Status is StudentFlowSubmissionStatus.Evaluated or StudentFlowSubmissionStatus.Submitted)
        {
            submission = new StudentFlowSubmission(command.UserId, StudentFlowKind.Onboarding, version.Id, command.SubmissionJson);
            _db.StudentFlowSubmissions.Add(submission);
        }
        else
        {
            submission.SaveDraft(command.SubmissionJson);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<SubmitOnboardingResult> HandleAsync(SubmitOnboardingCommand command, CancellationToken ct = default)
    {
        var version = await GetActivePublishedVersionAsync(ct)
            ?? throw new InvalidOperationException("No published onboarding template is available.");

        var data = ParseSubmissionData(command.SubmissionJson);

        // Server-side required-field enforcement — never trust the Form.io client alone.
        if (string.IsNullOrWhiteSpace(GetString(data, "preferred_name")))
            throw new OnboardingV2ValidationException("preferred_name is required.");

        var scoringRules = ParseScoringRules(version.ScoringRulesJson);
        foreach (var quickCheckKey in QuickCheckKeys)
        {
            if (scoringRules.ContainsKey(quickCheckKey) && string.IsNullOrWhiteSpace(GetString(data, quickCheckKey)))
                throw new OnboardingV2ValidationException($"{quickCheckKey} is required.");
        }

        var submission = await _db.StudentFlowSubmissions
            .Where(s => s.StudentId == command.UserId && s.FlowKind == StudentFlowKind.Onboarding)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (submission is null || submission.Status == StudentFlowSubmissionStatus.Evaluated)
        {
            submission = new StudentFlowSubmission(command.UserId, StudentFlowKind.Onboarding, version.Id, command.SubmissionJson);
            _db.StudentFlowSubmissions.Add(submission);
        }
        submission.MarkSubmitted(command.SubmissionJson);

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(sp => sp.UserId == command.UserId, ct);
        if (profile is not null)
            await ApplyToProfileAsync(profile, data, ct);

        var scores = new List<AssessmentScore>();
        foreach (var key in QuickCheckKeys)
        {
            if (!scoringRules.TryGetValue(key, out var rule)) continue;
            var submitted = GetString(data, key);
            var isCorrect = submitted is not null &&
                string.Equals(submitted, rule.CorrectAnswerKey, StringComparison.OrdinalIgnoreCase);
            scores.Add(new AssessmentScore(IsCorrect: isCorrect, Weight: 1));
        }

        var preliminaryCefr = PreliminaryCefrCalculator.Calculate(scores);

        if (profile is not null)
        {
            if (profile.CefrLevel is null && preliminaryCefr is not null)
                profile.SetCefrLevel(preliminaryCefr);

            if (profile.LifecycleStage is StudentLifecycleStage.OnboardingRequired or StudentLifecycleStage.OnboardingInProgress)
                profile.SetLifecycleStage(StudentLifecycleStage.PlacementRequired);

            profile.MarkOnboardingComplete();
        }

        submission.MarkEvaluated(JsonSerializer.Serialize(data.RootElement));

        await _db.SaveChangesAsync(ct);

        return new SubmitOnboardingResult(Success: true, PreliminaryCefrLevel: preliminaryCefr);
    }

    private async Task<StudentFlowTemplateVersion?> GetActivePublishedVersionAsync(CancellationToken ct)
    {
        // Avoid OrderBy on DateTimeOffset — SQLite (integration tests) does not support it.
        var template = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .Where(t => t.FlowKind == StudentFlowKind.Onboarding && t.Status == StudentFlowTemplateStatus.Published && t.ActiveVersionId != null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return template?.Versions.FirstOrDefault(v => v.Id == template.ActiveVersionId);
    }

    private async Task ApplyToProfileAsync(StudentProfile profile, JsonDocument data, CancellationToken ct)
    {
        var preferredName = GetString(data, "preferred_name");

        var supportCode = GetString(data, "support_language");
        supportCode = string.IsNullOrWhiteSpace(supportCode) || supportCode == "none" ? null : supportCode;
        var supportName = supportCode is null
            ? null
            : await _db.Languages.Where(l => l.Code == supportCode).Select(l => l.Name).FirstOrDefaultAsync(ct);
        var translationHelp = supportCode is null ? TranslationHelpPreference.Never : TranslationHelpPreference.WhenDifficult;

        var learningGoals = GetStringList(data, "learning_goals");
        var customLearningGoal = GetString(data, "custom_learning_goal");
        var focusAreas = GetStringList(data, "focus_areas");
        var customFocusArea = GetString(data, "custom_focus_area");

        var difficultyRaw = GetString(data, "difficulty_preference");
        var difficulty = difficultyRaw is not null && Enum.TryParse<DifficultyPreference>(difficultyRaw, ignoreCase: true, out var diff)
            ? diff
            : (DifficultyPreference?)null;

        var sessionDurationRaw = GetString(data, "session_duration");
        int? sessionDuration = sessionDurationRaw is not null && int.TryParse(sessionDurationRaw, out var minutes) && minutes > 0
            ? minutes
            : null;

        profile.UpdateLearningPreferences(
            preferredName: preferredName,
            supportLanguageCode: supportCode,
            supportLanguageName: supportName,
            translationHelpPreference: translationHelp,
            learningGoals: learningGoals,
            customLearningGoal: customLearningGoal,
            focusAreas: focusAreas,
            customFocusArea: customFocusArea,
            difficultyPreference: difficulty,
            preferredSessionDurationMinutes: sessionDuration);

        var careerContext = GetString(data, "career_context");
        if (careerContext is not null)
            profile.UpdateOnboardingFreeTextContext(careerContextText: careerContext, learningGoalDescription: null);

        var experienceRaw = GetString(data, "professional_experience_level");
        if (experienceRaw is not null && Enum.TryParse<ProfessionalExperienceLevel>(experienceRaw, ignoreCase: true, out var expLevel))
            profile.SetProfessionalExperienceLevel(expLevel);

        var familiarityRaw = GetString(data, "role_familiarity");
        if (familiarityRaw is not null && Enum.TryParse<RoleFamiliarity>(familiarityRaw, ignoreCase: true, out var familiarity))
            profile.SetRoleFamiliarity(familiarity);
    }

    private static JsonDocument ParseSubmissionData(string submissionJson)
    {
        try
        {
            var doc = JsonDocument.Parse(submissionJson);
            var root = doc.RootElement;
            // Accept either the raw Form.io "data" object directly, or a full submission
            // envelope ({ "data": {...} }) — the frontend always sends the former, but be lenient.
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataEl))
            {
                var inner = JsonDocument.Parse(dataEl.GetRawText());
                doc.Dispose();
                return inner;
            }
            return doc;
        }
        catch (JsonException ex)
        {
            throw new OnboardingV2ValidationException($"submissionJson is not valid JSON: {ex.Message}");
        }
    }

    private sealed record QuickCheckScoringRule(string? CorrectAnswerKey);

    private static Dictionary<string, QuickCheckScoringRule> ParseScoringRules(string? scoringRulesJson)
    {
        if (string.IsNullOrWhiteSpace(scoringRulesJson))
            return new Dictionary<string, QuickCheckScoringRule>();

        try
        {
            using var doc = JsonDocument.Parse(scoringRulesJson);
            var result = new Dictionary<string, QuickCheckScoringRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var key = prop.Value.TryGetProperty("correctAnswerKey", out var cak) && cak.ValueKind == JsonValueKind.String
                    ? cak.GetString()
                    : null;
                result[prop.Name] = new QuickCheckScoringRule(key);
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, QuickCheckScoringRule>();
        }
    }

    private static string? GetString(JsonDocument data, string key)
    {
        if (!data.RootElement.TryGetProperty(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static IReadOnlyList<string>? GetStringList(JsonDocument data, string key)
    {
        if (!data.RootElement.TryGetProperty(key, out var el)) return null;

        if (el.ValueKind == JsonValueKind.Array)
            return el.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();

        // Form.io "selectboxes" components submit { optionKey: true/false } by default.
        if (el.ValueKind == JsonValueKind.Object)
            return el.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.True)
                .Select(p => p.Name)
                .ToList();

        return null;
    }
}
