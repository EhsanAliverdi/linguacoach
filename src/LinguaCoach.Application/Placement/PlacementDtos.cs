using System.Text.Json.Serialization;

namespace LinguaCoach.Application.Placement;

/// <summary>A single question within a placement section.</summary>
public sealed record PlacementQuestionDto(
    string Key,
    string Prompt,
    /// <summary>One of: rating, choice, text.</summary>
    string Type,
    IReadOnlyList<string>? Options,
    /// <summary>
    /// Correct option for deterministic scoring. Never serialised to the student:
    /// the API sanitises sections to null and this is omitted when null.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CorrectOption = null);

/// <summary>A placement section definition (content + questions).</summary>
public sealed record PlacementSectionDto(
    string Key,
    int Order,
    string Title,
    string Instructions,
    /// <summary>One of: self_check, mcq, reading, listening, writing, speaking.</summary>
    string SectionType,
    bool Scored,
    IReadOnlyList<PlacementQuestionDto> Questions,
    string? Passage,
    string? AudioScript,
    string? WritingPrompt,
    string? SpeakingPrompt);

/// <summary>A student's submitted answer for one question.</summary>
public sealed record PlacementAnswerDto(
    string QuestionKey,
    string? ResponseText,
    string? SelectedOption);

/// <summary>Current placement state for routing and resume.</summary>
public sealed record PlacementStatusDto(
    /// <summary>NotStarted, InProgress, Completed.</summary>
    string Status,
    string CurrentSectionKey,
    int CurrentSectionOrder,
    int TotalSections,
    string LifecycleStage,
    bool IsCompleted);

/// <summary>The current section the student should complete (questions stripped of correct answers).</summary>
public sealed record PlacementCurrentSectionDto(
    string Status,
    PlacementSectionDto? Section,
    int CurrentSectionOrder,
    int TotalSections,
    bool IsCompleted);

/// <summary>Per-skill CEFR level pair for the result view.</summary>
public sealed record PlacementSkillLevelDto(string Skill, string Level);

/// <summary>The final placement result shown to the student.</summary>
public sealed record PlacementResultDto(
    string EstimatedOverallLevel,
    IReadOnlyList<PlacementSkillLevelDto> SkillLevels,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string? RecommendedStartingCourse,
    int? RecommendedSessionDuration,
    string? PlacementNotes,
    bool IsCompleted);
