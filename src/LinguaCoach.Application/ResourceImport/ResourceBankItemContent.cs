using System.Text.Json;

namespace LinguaCoach.Application.ResourceImport;

/// <summary>
/// Phase I0 — the type-specific payload shapes packed into <see cref="Domain.Entities.ResourceBankItem.ContentJson"/>.
/// Common/filterable fields (CefrLevel/Subskill/DifficultyBand/tags) stay real columns on the
/// entity itself; only the genuinely per-type fields live here. Lives in Application (not
/// Infrastructure) so both Infrastructure and Persistence can reference it without violating the
/// Domain ← Application ← {Infrastructure, Persistence} dependency rule.
/// </summary>
public sealed record VocabularyContent(string Word, string? PartOfSpeech, string? Notes);

public sealed record GrammarContent(string GrammarPoint, string? Description);

public sealed record ReadingReferenceContent(string? TextType, string? DifficultyNotes, string? ReferenceExcerpt);

public sealed record ReadingPassageContent(
    string Title,
    string PassageText,
    string? Summary,
    string PrimarySkill,
    string? TopicTagsJson,
    int WordCount,
    int EstimatedReadingMinutes,
    string? AttributionText,
    double? QualityScore);

public static class ResourceBankItemContent
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T content) => JsonSerializer.Serialize(content, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"ResourceBankItem.ContentJson could not be parsed as {typeof(T).Name}.");
}
