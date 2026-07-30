namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Grammar Skill Graph Phase GSG-1 (2026-07-31) — how much evidence backs a
/// <see cref="Entities.SkillGraphNode"/>'s <c>CefrLevel</c>. Introduced after the 2026-07-30 seed
/// audit found 145 of 592 CEFR-J-derived grammar nodes carried an A1 label with no real evidence
/// (either silently defaulted, or resolved via a non-CEFR-J fallback column) — see
/// docs/reviews/2026-07-30-grammar-skill-graph-seed-audit.md. Consulted by
/// <see cref="Entities.SkillGraphNode.RoutingEligible"/>: only Attested/Curated nodes may default
/// to routing-eligible.
/// </summary>
public enum CefrConfidence
{
    /// <summary>Not yet classified. Should not persist past a provenance backfill — a node stuck
    /// here means nobody has run the classification pass on it.</summary>
    Unknown = 0,

    /// <summary>Resolved via a non-CEFR-J framework column (Core Inventory/EGP/GSELO), or via no
    /// column at all (silently defaulted to A1 by the pre-GSG-1 importer). See
    /// <see cref="Entities.SkillGraphNode.CefrSource"/> to distinguish which.</summary>
    Fallback = 1,

    /// <summary>Hand-authored node whose CEFR level was copied from a parent/category with no
    /// independent evidence of its own (e.g. the 2026-07-30 Adverbs pilot's item leaves, all
    /// inheriting their subtopic container's level).</summary>
    Inherited = 2,

    /// <summary>The CEFR-J Level column itself gave this level directly — the strongest evidence
    /// this dataset can produce.</summary>
    Attested = 3,

    /// <summary>A human or AI reviewer has since confirmed or corrected this node's level as part
    /// of a curriculum-curation pass (Phase GSG-4+).</summary>
    Curated = 4,
}
