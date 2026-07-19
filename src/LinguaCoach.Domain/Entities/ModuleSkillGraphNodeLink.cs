using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Adaptive Curriculum Sprint 2 — many-to-many coverage link: which <see cref="SkillGraphNode"/>(s)
/// a <see cref="Module"/> teaches or practices. Tagged at Module granularity only (not Lesson/
/// Exercise) — Module is the unit <c>ITodayPlanModuleSelectionService</c>/
/// <c>IPracticeGymModuleSelectionService</c> actually select, and the existing content base is too
/// small yet (2 approved Modules at Sprint 2 time) to justify finer-grained tagging. Follows
/// <see cref="ModuleLessonLink"/>'s join-table convention.
/// </summary>
public sealed class ModuleSkillGraphNodeLink : BaseEntity
{
    public Guid ModuleId { get; private set; }
    public Guid SkillGraphNodeId { get; private set; }

    /// <summary>0-1 confidence from the AI re-tagging pass that proposed this link, or null when the
    /// link was added manually by an admin. Never used to gate anything in Sprint 2 — kept purely
    /// for the coverage-gap dashboard's "how confident was this" context.</summary>
    public double? Confidence { get; private set; }

    private ModuleSkillGraphNodeLink() { }

    public ModuleSkillGraphNodeLink(Guid moduleId, Guid skillGraphNodeId, double? confidence = null)
    {
        if (moduleId == Guid.Empty)
            throw new ArgumentException("ModuleId must not be empty.", nameof(moduleId));
        if (skillGraphNodeId == Guid.Empty)
            throw new ArgumentException("SkillGraphNodeId must not be empty.", nameof(skillGraphNodeId));
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        ModuleId = moduleId;
        SkillGraphNodeId = skillGraphNodeId;
        Confidence = confidence;
    }
}
