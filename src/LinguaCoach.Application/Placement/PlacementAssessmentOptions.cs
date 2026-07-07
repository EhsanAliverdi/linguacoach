namespace LinguaCoach.Application.Placement;

public sealed class PlacementAssessmentOptions
{
    public const string Section = "PlacementAssessment";
    public int MinItems { get; set; } = 5;
    // Must comfortably exceed MinItems × SkillsToAssess.Length (6 skills × 5 = 30 minimum) or the
    // global cap is hit before every skill gets even its minimum look, silently marking untested
    // skills "100% complete" with a fallback CEFR level (see GetSkillStatusAsync). Real convergence
    // observed around 6-7 items/skill (confidence formula reaches the 0.75 threshold near 6 items
    // at a good score), so 48 gives headroom above the ~42-item typical case.
    public int MaxItems { get; set; } = 48;
    public double ConfidenceThreshold { get; set; } = 0.75;
    public string[] SkillsToAssess { get; set; } = ["listening", "reading", "writing", "vocabulary", "grammar", "speaking"];
    public string StartingLevelFallback { get; set; } = "A2";
    public int AssessmentExpiryMinutes { get; set; } = 60;

    // Phase 14A — Student placement gate configuration
    public bool PlacementRequiredBeforeLearning { get; set; } = true;
    public bool AllowSkipPlacement { get; set; } = false;
    public bool AllowPlacementRetake { get; set; } = false;
    public bool ResumeInterruptedPlacement { get; set; } = true;
    public bool AutoStartPlacement { get; set; } = false;

    /// <summary>Minimum ISpeakingEvaluationProvider OverallScore (0..1) for a speaking placement
    /// item to count as correct/passed.</summary>
    public double SpeakingPassThreshold { get; set; } = 0.6;
}
