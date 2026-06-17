using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Aggregated daily usage counters per student. Updated after each usage event.
/// </summary>
public sealed class StudentUsageDaily : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public DateOnly Date { get; private set; }

    public int TotalTokens { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal TotalCost { get; private set; }
    public int AiCallCount { get; private set; }
    public decimal LiveAiMinutes { get; private set; }
    public int TtsCharacters { get; private set; }
    public decimal SttMinutes { get; private set; }

    public int LessonGenerations { get; private set; }
    public int PracticeGenerations { get; private set; }
    public int WritingEvaluations { get; private set; }
    public int SpeakingEvaluations { get; private set; }
    public int PreparedActivitiesCompleted { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private StudentUsageDaily()
    {
    }

    public StudentUsageDaily(Guid studentProfileId, DateOnly date)
    {
        StudentProfileId = studentProfileId;
        Date = date;
        UpdatedAt = CreatedAt;
    }

    public void Apply(
        int inputTokens,
        int outputTokens,
        int totalTokens,
        decimal cost,
        bool isAiCall,
        decimal liveAiMinutes,
        int ttsCharacters,
        decimal sttMinutes,
        string featureKey)
    {
        InputTokens += inputTokens;
        OutputTokens += outputTokens;
        TotalTokens += totalTokens;
        TotalCost += cost;
        if (isAiCall) AiCallCount++;
        LiveAiMinutes += liveAiMinutes;
        TtsCharacters += ttsCharacters;
        SttMinutes += sttMinutes;

        switch (featureKey)
        {
            case "lesson.generate":
            case "lesson.regenerate":
                LessonGenerations++;
                break;
            case "practice.dynamic.generate":
                PracticeGenerations++;
                break;
            case "writing.evaluate":
                WritingEvaluations++;
                break;
            case "speaking.evaluate":
                SpeakingEvaluations++;
                break;
            case "practice.prepared.complete":
            case "lesson.complete":
                PreparedActivitiesCompleted++;
                break;
        }

        UpdatedAt = DateTime.UtcNow;
    }
}
