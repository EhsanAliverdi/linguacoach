using System.Linq.Expressions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Modules;

/// <summary>
/// Phase 1 (2026-07-15 pipeline safety audit) — the single shared predicate for whether a
/// <see cref="Module"/> is available for <b>new</b> student delivery (Today selection, Practice
/// Gym selection, and launch-time re-validation). Previously each of those three call sites
/// inlined its own <c>ReviewStatus == Approved</c> check and none of them also checked
/// <see cref="Module.IsArchived"/>, so an approved-but-archived Module could still be
/// suggested/assigned/launched — <see cref="Module.IsArchived"/>'s doc comment only promises
/// that archiving won't disturb <i>existing</i> assignments, not that it blocks new ones.
///
/// This helper governs new-selection/new-assignment/new-launch eligibility only. It does not
/// decide whether an already-created assignment or <see cref="StudentExerciseLaunch"/> may still
/// resolve/complete — that policy is unchanged and intentionally untouched by archival.
/// </summary>
public static class ModuleEligibility
{
    /// <summary>EF-translatable form — use in <c>.Where(...)</c> against <c>IQueryable&lt;Module&gt;</c>
    /// so the archived/approved filter is applied server-side, e.g.
    /// <c>_db.Modules.Where(ModuleEligibility.AvailableForNewStudentDeliveryExpr)</c>.</summary>
    public static readonly Expression<Func<Module, bool>> AvailableForNewStudentDeliveryExpr =
        module => module.ReviewStatus == AdminReviewStatus.Approved && !module.IsArchived;

    private static readonly Func<Module, bool> Compiled = AvailableForNewStudentDeliveryExpr.Compile();

    /// <summary>True when an already-materialized Module may be suggested, newly assigned, or
    /// newly launched. Use <see cref="AvailableForNewStudentDeliveryExpr"/> instead inside an EF
    /// query so the filter runs server-side.</summary>
    public static bool IsAvailableForNewStudentDelivery(Module module) => Compiled(module);
}
