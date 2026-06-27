namespace LinguaCoach.Application.Mastery;

/// <summary>Why a mastery evaluation was triggered.</summary>
public enum MasteryEvaluationReason
{
    Manual = 0,
    AfterPracticeGym = 1,
    AfterTodayLesson = 2,
    ScheduledSweep = 3,
    BeforeReplenishment = 4,
    PlanGeneration = 5
}
