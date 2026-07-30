namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Grammar Skill Graph Phase GSG-1 (2026-07-31) — what kind of thing a
/// <see cref="Entities.SkillGraphNode"/> actually is, distinct from the container/leaf
/// (<c>ParentNodeId</c>) hierarchy shape. The 2026-07-30 seed audit found the graph mixes very
/// narrow, independently assessable items ("always", "I am") with entire grammatical domains
/// compressed into one standalone leaf ("PREPOSITIONS") — see
/// docs/reviews/2026-07-30-grammar-skill-graph-seed-audit.md §9. Only Skill/Variant nodes should
/// ever be eligible for learner mastery or routing.
/// </summary>
public enum SkillGraphNodeType
{
    /// <summary>A container that itself parents other containers — a topical grouping one level
    /// above individual grammar concepts (e.g. "Adverbs" parenting "Adverbs of frequency").</summary>
    Topic = 0,

    /// <summary>A container that parents only leaves — a "family" container (affirmative/negative/
    /// question forms of one grammar point) or a subtopic container.</summary>
    Concept = 1,

    /// <summary>An independently assessable leaf — the unit learner mastery should attach to.</summary>
    Skill = 2,

    /// <summary>A leaf that is one grammatical form of a family (the Affirmative/Negative/Question/
    /// Negative question children of a Concept container) — assessable, but only meaningful in the
    /// context of its sibling forms.</summary>
    Variant = 3,

    /// <summary>A standalone leaf whose real-world scope is too broad to be independently
    /// masterable as a single unit (e.g. "PREPOSITIONS", "REFLEXIVE PRONOUNS") — not routing- or
    /// mastery-eligible until split into narrower nodes.</summary>
    BroadReference = 4,
}
