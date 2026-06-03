using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records a student's writing exercise attempt and the AI feedback received.
/// One row per submission — simple append-only log for MVP.
/// </summary>
public sealed class WritingSubmission : BaseEntity
{
    public Guid StudentProfileId { get; private set; }

    // FK to WritingScenario — nullable so pre-migration submissions are not broken.
    public Guid? ScenarioId { get; private set; }

    // Short human-readable scenario title, e.g. "Follow-up email for pending approval".
    public string ScenarioTitle { get; private set; }

    // The student's submitted draft text.
    public string OriginalText { get; private set; }

    // AI-corrected version of the email (may be empty if AI call fails).
    public string CorrectedText { get; private set; }

    // Full structured feedback from AI, stored as JSON for display flexibility.
    public string FeedbackJson { get; private set; }

    // Numeric score 0–100 from the AI response. Null if AI returned no score.
    public double? Score { get; private set; }

    // Which prompt key was used, for auditability.
    public string PromptKey { get; private set; }

    private WritingSubmission()
    {
        ScenarioTitle = string.Empty;
        OriginalText = string.Empty;
        CorrectedText = string.Empty;
        FeedbackJson = "{}";
        PromptKey = string.Empty;
    }

    public WritingSubmission(
        Guid studentProfileId,
        string scenarioTitle,
        string originalText,
        string correctedText,
        string feedbackJson,
        double? score,
        string promptKey,
        Guid? scenarioId = null)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(scenarioTitle)) throw new ArgumentException("ScenarioTitle is required.", nameof(scenarioTitle));
        if (string.IsNullOrWhiteSpace(originalText)) throw new ArgumentException("OriginalText is required.", nameof(originalText));
        if (score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");

        StudentProfileId = studentProfileId;
        ScenarioId = scenarioId;
        ScenarioTitle = scenarioTitle.Trim();
        OriginalText = originalText.Trim();
        CorrectedText = correctedText?.Trim() ?? string.Empty;
        FeedbackJson = string.IsNullOrWhiteSpace(feedbackJson) ? "{}" : feedbackJson;
        Score = score;
        PromptKey = promptKey?.Trim() ?? string.Empty;
    }
}
