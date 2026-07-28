namespace LinguaCoach.Application.SkillGraph;

/// <summary>Full content reseed (2026-07-28) — deterministic (no AI) parsing of the real CEFR-J
/// and Octanove vocabulary profile CSVs into leaf-level <c>SkillGraphNode</c> proposals, mirroring
/// <c>ICefrJGrammarImportService</c>'s shape/discipline. Container assignment here is only the
/// deterministic half: real topic categories already present in ~22% of CEFR-J rows (the
/// <c>CoreInventory 1</c> column) become containers directly; every other word is returned as
/// <see cref="VocabularyProposedLeaf"/> with <c>ParentKey</c> null, needing a separate AI
/// categorization pass (not in this service — deterministic parsing and AI classification are
/// deliberately separate steps, same "propose, never auto-apply silently" discipline as the rest
/// of this codebase's AI features) before every word has a container.</summary>
public interface IVocabularyImportService
{
    VocabularyImportPreview ParseCsvFiles(string cefrJVocabularyCsv, string octanoveVocabularyCsv);
}

/// <summary>One parsed CSV row, before any container/description/tag decision — the raw unit both
/// the deterministic categorization pass and the (separate) AI categorization pass operate over.</summary>
public sealed record VocabularyWordRow(
    string Headword,
    string PartOfSpeech,
    string CefrLevel,
    /// <summary>Normalized <c>CoreInventory 1</c> topic category, or null if the source row had
    /// none — CEFR-J leaves this blank on ~78% of rows; Octanove has no category column at all.</summary>
    string? Category);

public sealed record VocabularyImportPreview(
    IReadOnlyList<VocabularyProposedContainer> CategorizedContainers,
    IReadOnlyList<VocabularyProposedLeaf> UncategorizedLeaves,
    IReadOnlyList<string> Warnings)
{
    public int TotalLeafCount => CategorizedContainers.Sum(c => c.Leaves.Count) + UncategorizedLeaves.Count;
}

/// <summary>One real topic category (from CEFR-J's own <c>CoreInventory 1</c> column, normalized —
/// e.g. "Work and Jobs"/"Work and jobs" collapsed to one). Spans every CEFR level its leaves cover
/// (a topic like "Education" legitimately has A2 through C1 words) — <c>CefrLevel</c> here is the
/// container's own easiest-entry-point level, the same convention leaf-less containers elsewhere
/// in this codebase use; it is not a filter on which leaves belong under it.</summary>
public sealed record VocabularyProposedContainer(
    string Key,
    string Title,
    string CefrLevel,
    IReadOnlyList<VocabularyProposedLeaf> Leaves);

/// <summary>One vocabulary word → one leaf. <c>Description</c> is a deterministic placeholder
/// ("real definition pending AI pass") — the CSV carries no definitions, so a real student-facing
/// description is generated later, not invented here without a real source.</summary>
public sealed record VocabularyProposedLeaf(
    string Key,
    string Title,
    string CefrLevel,
    string Headword,
    string PartOfSpeech,
    string Description);
