namespace LinguaCoach.Domain.Enums;

/// <summary>Phase 4 — coarse detected media category for an <see cref="Entities.ImportAsset"/>,
/// derived from MIME type/extension during manifest inspection.</summary>
public enum ImportAssetMediaType
{
    Unknown = 0,
    Text = 1,
    StructuredData = 2, // CSV/JSON/JSONL/XML
    Audio = 3,
    Image = 4,
    Video = 5,
    Archive = 6, // nested archive — rejected beyond configured nesting depth, see limits
    Executable = 7 // always rejected
}
