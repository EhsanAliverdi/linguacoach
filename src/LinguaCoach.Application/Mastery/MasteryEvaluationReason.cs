namespace LinguaCoach.Application.Mastery;

/// <summary>Why a mastery evaluation was triggered.</summary>
public enum MasteryEvaluationReason
{
    Manual = 0,
    AfterPracticeGym = 1,
    AfterTodayLesson = 2,
    ScheduledSweep = 3,
    BeforeReplenishment = 4,
    PlanGeneration = 5,
    /// <summary>Adaptive Curriculum Sprint 5 — evaluated on-demand by the AI composer to flag
    /// weakness-match candidates for Today/Practice Gym ranking.</summary>
    ContentDelivery = 6
}
