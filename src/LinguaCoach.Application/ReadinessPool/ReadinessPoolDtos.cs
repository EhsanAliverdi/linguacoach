using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Input used to create a queued readiness pool item from a routing recommendation snapshot.
/// </summary>
public sealed class CreateReadinessItemRequest
{
    public required Guid StudentId { get; init; }
    public required ReadinessPoolSource Source { get; init; }
    public required string TargetCefrLevel { get; init; }
    public RoutingReason RoutingReason { get; init; } = RoutingReason.Normal;
    public bool IsLowerLevelContent { get; init; }
    public string? CurriculumObjectiveKey { get; init; }
    public string? CurriculumObjectiveTitle { get; init; }
    public string? PrimarySkill { get; init; }
    public string SecondarySkillsJson { get; init; } = "[]";
    public string ContextTagsJson { get; init; } = "[]";
    public string FocusTagsJson { get; init; } = "[]";
    public string? PatternKey { get; init; }
    public string? ActivityType { get; init; }
    public int DifficultyBand { get; init; } = 1;
    public string? OriginalCefrLevelSnapshot { get; init; }
    public string? RoutingExplanation { get; init; }
    public int? PreferredSessionDurationMinutes { get; init; }
    public string? DifficultyPreference { get; init; }
    public string? SupportLanguageCode { get; init; }
    public string? SupportLanguageName { get; init; }
    public string? TranslationHelpPreference { get; init; }
    public string? GeneratedBy { get; init; }
    public int Priority { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Summary DTO for pool inspection (admin read-only, future student-facing metrics).
/// </summary>
public sealed class ReadinessPoolSummary
{
    public Guid StudentId { get; init; }
    public int QueuedCount { get; init; }
    public int GeneratingCount { get; init; }
    public int ReadyCount { get; init; }
    public int ReservedCount { get; init; }
    public int ConsumedCount { get; init; }
    public int ExpiredCount { get; init; }
    public int FailedCount { get; init; }
    public int StaleCount { get; init; }
    public int ReviewOnlyCount { get; init; }
    public IReadOnlyList<ReadinessItemDto> Items { get; init; } = [];
}

public sealed class ReadinessItemDto
{
    public Guid Id { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string TargetCefrLevel { get; init; } = string.Empty;
    public string? CurriculumObjectiveKey { get; init; }
    public string? CurriculumObjectiveTitle { get; init; }
    public string? PrimarySkill { get; init; }
    public string RoutingReason { get; init; } = string.Empty;
    public bool IsLowerLevelContent { get; init; }
    public string? PatternKey { get; init; }
    public string? ActivityType { get; init; }
    public int DifficultyBand { get; init; }
    public Guid? LearningSessionId { get; init; }
    public Guid? LearningActivityId { get; init; }
    public int AttemptCount { get; init; }
    public string? ErrorCode { get; init; }
    public DateTime? ReservedAt { get; init; }
    public DateTime? ConsumedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Helper to build a CreateReadinessItemRequest from a CurriculumRoutingRecommendation + student context.
/// </summary>
public static class ReadinessItemRequestBuilder
{
    public static CreateReadinessItemRequest FromRoutingRecommendation(
        Guid studentId,
        ReadinessPoolSource source,
        CurriculumRoutingRecommendation recommendation,
        string? originalCefrLevelSnapshot = null,
        int? preferredSessionDurationMinutes = null,
        string? difficultyPreference = null,
        string? supportLanguageCode = null,
        string? supportLanguageName = null,
        string? translationHelpPreference = null,
        string? patternKey = null,
        string? activityType = null,
        string? generatedBy = null,
        int priority = 0,
        DateTime? expiresAt = null)
    {
        return new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = source,
            TargetCefrLevel = recommendation.TargetCefrLevel,
            RoutingReason = recommendation.RoutingReason,
            IsLowerLevelContent = recommendation.IsLowerLevelContent,
            CurriculumObjectiveKey = recommendation.CurriculumObjectiveKey,
            CurriculumObjectiveTitle = recommendation.CurriculumObjectiveTitle,
            PrimarySkill = recommendation.PrimarySkill,
            SecondarySkillsJson = System.Text.Json.JsonSerializer.Serialize(recommendation.SecondarySkills),
            ContextTagsJson = System.Text.Json.JsonSerializer.Serialize(recommendation.ContextTags),
            FocusTagsJson = System.Text.Json.JsonSerializer.Serialize(recommendation.FocusTags),
            DifficultyBand = recommendation.DifficultyBand,
            OriginalCefrLevelSnapshot = originalCefrLevelSnapshot,
            RoutingExplanation = recommendation.Explanation,
            PatternKey = patternKey,
            ActivityType = activityType,
            PreferredSessionDurationMinutes = preferredSessionDurationMinutes,
            DifficultyPreference = difficultyPreference,
            SupportLanguageCode = supportLanguageCode,
            SupportLanguageName = supportLanguageName,
            TranslationHelpPreference = translationHelpPreference,
            GeneratedBy = generatedBy,
            Priority = priority,
            ExpiresAt = expiresAt
        };
    }
}
