using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Mastery;

/// <summary>Skill Graph container/leaf Phase 3 (2026-07-27) — a container node's mastery display is
/// a pure read-time aggregation over its Approved+Active leaf children's real per-node mastery, not
/// a persisted status. Reversible by construction: nothing is stored, so the rollup rule can change
/// later without a data migration.</summary>
public sealed record ContainerMasteryRollup(
    Guid ContainerNodeId,
    int TotalLeafCount,
    int MasteredLeafCount,
    double PercentMastered,
    MasteryStatus RollupStatus);
