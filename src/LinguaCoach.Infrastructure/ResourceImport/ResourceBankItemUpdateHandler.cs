using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase K5 — admin edit of a published Resource Bank item's content/metadata. Every branch
/// requires the fields relevant to that item's own <see cref="PublishedResourceType"/> and
/// re-serializes a fresh <see cref="ResourceBankItemContent"/> record (full-replace, not a partial
/// patch — same PUT convention as Lesson/Exercise/Module). Fields the edit form never exposes
/// (ReadingPassage's PrimarySkill/TopicTagsJson/AttributionText/QualityScore, Listening's
/// AudioStorageKey/AudioContentType/AttributionText) are carried forward unchanged from the
/// existing row rather than dropped.
/// </summary>
public sealed class ResourceBankItemUpdateHandler : IResourceBankItemUpdateHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IResourceBankQueryService _queryService;

    public ResourceBankItemUpdateHandler(LinguaCoachDbContext db, IResourceBankQueryService queryService)
    {
        _db = db;
        _queryService = queryService;
    }

    public async Task<UnifiedResourceBankItemDto> HandleAsync(UpdateResourceBankItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.ResourceBankItems.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new ResourceImportValidationException($"Resource Bank item '{command.Id}' was not found.");

        string contentJson;
        switch (item.Type)
        {
            case PublishedResourceType.Vocabulary:
                if (string.IsNullOrWhiteSpace(command.Word))
                    throw new ResourceImportValidationException("Word is required.");
                contentJson = ResourceBankItemContent.Serialize(
                    new VocabularyContent(command.Word.Trim(), command.PartOfSpeech?.Trim(), command.Notes?.Trim()));
                break;

            case PublishedResourceType.Grammar:
                if (string.IsNullOrWhiteSpace(command.GrammarPoint))
                    throw new ResourceImportValidationException("GrammarPoint is required.");
                contentJson = ResourceBankItemContent.Serialize(
                    new GrammarContent(command.GrammarPoint.Trim(), command.Description?.Trim()));
                break;

            case PublishedResourceType.ReadingReference:
                if (string.IsNullOrWhiteSpace(command.ReferenceExcerpt))
                    throw new ResourceImportValidationException("ReferenceExcerpt is required.");
                contentJson = ResourceBankItemContent.Serialize(new ReadingReferenceContent(
                    command.TextType?.Trim(), command.DifficultyNotes?.Trim(), command.ReferenceExcerpt.Trim()));
                break;

            case PublishedResourceType.ReadingPassage:
            {
                if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.PassageText))
                    throw new ResourceImportValidationException("Title and PassageText are required.");
                var existing = ResourceBankItemContent.Deserialize<ReadingPassageContent>(item.ContentJson);
                var passage = command.PassageText.Trim();
                var wordCount = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                var estimatedReadingMinutes = Math.Max(1, (int)Math.Round(wordCount / 200.0, MidpointRounding.AwayFromZero));
                contentJson = ResourceBankItemContent.Serialize(new ReadingPassageContent(
                    command.Title.Trim(), passage, command.Summary?.Trim(), existing.PrimarySkill, existing.TopicTagsJson,
                    wordCount, estimatedReadingMinutes, existing.AttributionText, existing.QualityScore));
                break;
            }

            case PublishedResourceType.Writing:
                if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.PromptText))
                    throw new ResourceImportValidationException("Title and PromptText are required.");
                contentJson = ResourceBankItemContent.Serialize(new WritingPromptContent(
                    command.Title.Trim(), command.PromptText.Trim(), command.Genre?.Trim(), command.SuggestedMinWords));
                break;

            case PublishedResourceType.Listening:
            {
                if (string.IsNullOrWhiteSpace(command.Title))
                    throw new ResourceImportValidationException("Title is required.");
                var existing = ResourceBankItemContent.Deserialize<ListeningPassageContent>(item.ContentJson);
                contentJson = ResourceBankItemContent.Serialize(new ListeningPassageContent(
                    command.Title.Trim(), command.Transcript?.Trim(), existing.AudioStorageKey, existing.AudioContentType,
                    existing.AttributionText));
                break;
            }

            case PublishedResourceType.Speaking:
                if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.PromptText))
                    throw new ResourceImportValidationException("Title and PromptText are required.");
                contentJson = ResourceBankItemContent.Serialize(new SpeakingPromptContent(
                    command.Title.Trim(), command.PromptText.Trim(), command.SuggestedDurationSeconds,
                    string.IsNullOrWhiteSpace(command.ImageUrl) ? null : command.ImageUrl.Trim()));
                break;

            default:
                throw new ResourceImportValidationException($"Editing Resource Bank items of type '{item.Type}' is not supported.");
        }

        try
        {
            item.UpdateContent(
                command.CefrLevel, contentJson, command.Subskill, command.DifficultyBand,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ResourceImportValidationException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new ResourceImportValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return (await _queryService.GetUnifiedByIdAsync(command.Id, ct))!;
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(values) : "[]";
}
