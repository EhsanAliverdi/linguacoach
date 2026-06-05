namespace LinguaCoach.Domain.ValueObjects;

public sealed record MemoryUpdateDelta(
    string? JourneySummaryDelta,
    IReadOnlyList<string> NewStrengths,
    IReadOnlyList<string> NewWeaknesses,
    IReadOnlyList<string> RecurringMistakesToAdd,
    IReadOnlyList<string> CoveredScenariosToAdd,
    IReadOnlyList<string> WeakSkillKeys,
    IReadOnlyList<string> StrongSkillKeys,
    IReadOnlyList<string> RecommendedNextFocus);
