using LinguaCoach.Application.AdminRepair;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.AdminRepair;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase K8/K9 — diagnoses and AI-repairs the single most common Resource Bank data gap per type:
/// a missing core descriptive field (Vocabulary's definition, Grammar's description, etc.) that
/// otherwise silently blocks downstream Lesson/Exercise generation. Never touches CefrLevel,
/// tags, or any other structural field — only fills the one missing text field per type.
/// </summary>
public sealed class ResourceBankRepairService : IResourceBankRepairService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IResourceBankQueryService _queryService;
    private readonly AdminRepairFieldGenerator _fieldGenerator;

    public ResourceBankRepairService(
        LinguaCoachDbContext db, IResourceBankQueryService queryService, AdminRepairFieldGenerator fieldGenerator)
    {
        _db = db;
        _queryService = queryService;
        _fieldGenerator = fieldGenerator;
    }

    public async Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.ResourceBankItems.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new ResourceImportValidationException($"Resource Bank item '{id}' was not found.");
        return Diagnose(entity);
    }

    public async Task<ResourceBankItemRepairResult> RepairAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.ResourceBankItems.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new ResourceImportValidationException($"Resource Bank item '{id}' was not found.");

        var issues = Diagnose(entity);
        var fixable = issues.Where(i => i.AutoFixable).ToList();
        if (fixable.Count == 0)
            throw new ResourceImportValidationException("Nothing to repair — no auto-fixable issues were found on this item.");

        var (fixedIssues, providerName, modelName) = await ApplyRepairAsync(entity, issues, ct);
        await _db.SaveChangesAsync(ct);

        var dto = await _queryService.GetUnifiedByIdAsync(id, ct)
            ?? throw new ResourceImportValidationException($"Resource Bank item '{id}' disappeared during repair.");
        var remaining = issues.Where(i => !fixedIssues.Contains(i)).ToList();
        return new ResourceBankItemRepairResult(dto, fixedIssues, remaining, providerName, modelName);
    }

    public async Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default)
    {
        var entities = await _db.ResourceBankItems.AsNoTracking().Where(e => !e.IsArchived).ToListAsync(ct);
        var withIssues = entities.Count(e => Diagnose(e).Any(i => i.AutoFixable));
        return new IssuesSummary(entities.Count, withIssues);
    }

    public async Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default)
    {
        var entities = await _db.ResourceBankItems.AsNoTracking().Where(e => !e.IsArchived).ToListAsync(ct);
        return entities
            .Where(e => Diagnose(e).Any(i => i.AutoFixable))
            .Select(e => new RepairableItemSummary(e.Id, TitleFor(e)))
            .ToList();
    }

    public async Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default)
    {
        var entities = await _db.ResourceBankItems.Where(e => !e.IsArchived).ToListAsync(ct);
        var errors = new List<string>();
        var withIssues = 0;
        var repaired = 0;

        foreach (var entity in entities)
        {
            var issues = Diagnose(entity);
            if (!issues.Any(i => i.AutoFixable)) continue;
            withIssues++;
            try
            {
                await ApplyRepairAsync(entity, issues, ct);
                repaired++;
            }
            catch (Exception ex)
            {
                errors.Add($"{TitleFor(entity)}: {ex.Message}");
            }
        }

        if (repaired > 0)
            await _db.SaveChangesAsync(ct);

        return new BulkRepairResult(entities.Count, withIssues, repaired, withIssues - repaired, errors);
    }

    /// <summary>Mutates <paramref name="entity"/> in place (caller is responsible for
    /// SaveChangesAsync) and returns which of <paramref name="issues"/> were fixed plus the AI
    /// provider/model used, if any.</summary>
    private async Task<(List<DiagnosticIssue> FixedIssues, string? ProviderName, string? ModelName)> ApplyRepairAsync(
        ResourceBankItem entity, List<DiagnosticIssue> issues, CancellationToken ct)
    {
        string? providerName = null;
        string? modelName = null;
        var fixedIssues = new List<DiagnosticIssue>();

        switch (entity.Type)
        {
            case PublishedResourceType.Vocabulary:
            {
                var c = ResourceBankItemContent.Deserialize<VocabularyContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.Notes))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Vocabulary word", "a concise dictionary-style definition (one sentence, no examples)",
                        $"Word: \"{c.Word}\". Part of speech: {c.PartOfSpeech ?? "unspecified"}. CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { Notes = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_definition"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
            case PublishedResourceType.Grammar:
            {
                var c = ResourceBankItemContent.Deserialize<GrammarContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.Description))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Grammar point", "a concise explanation (1-2 sentences) of how this grammar point works",
                        $"Grammar point: \"{c.GrammarPoint}\". CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { Description = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_description"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
            case PublishedResourceType.ReadingReference:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.ReferenceExcerpt))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Reading reference", "a short (2-3 sentence) illustrative excerpt suitable for a comprehension question",
                        $"Text type: {c.TextType ?? "unspecified"}. CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { ReferenceExcerpt = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_excerpt"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
            case PublishedResourceType.ReadingPassage:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.Summary))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Reading passage", "a one-sentence summary of the passage",
                        $"Title: \"{c.Title}\". Passage: {Truncate(c.PassageText)} CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { Summary = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_summary"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
            case PublishedResourceType.Writing:
            {
                var c = ResourceBankItemContent.Deserialize<WritingPromptContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.PromptText))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Writing prompt", "a short writing task prompt (1-2 sentences)",
                        $"Title: \"{c.Title}\". Genre: {c.Genre ?? "unspecified"}. CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { PromptText = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_prompt_text"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
            case PublishedResourceType.Listening:
            {
                var c = ResourceBankItemContent.Deserialize<ListeningPassageContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.Transcript))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Listening passage", "a short (2-4 sentence) transcript matching the title",
                        $"Title: \"{c.Title}\". CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { Transcript = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_transcript"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
            case PublishedResourceType.Speaking:
            {
                var c = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(c.PromptText))
                {
                    var field = await _fieldGenerator.GenerateFieldAsync(
                        "Speaking prompt", "a short role-play/task scenario prompt (1-2 sentences)",
                        $"Title: \"{c.Title}\". CEFR level: {entity.CefrLevel}.", ct);
                    c = c with { PromptText = field.Value };
                    (providerName, modelName) = (field.ProviderName, field.ModelName);
                    fixedIssues.Add(issues.First(i => i.Code == "missing_prompt_text"));
                }
                entity.UpdateContent(entity.CefrLevel, ResourceBankItemContent.Serialize(c), entity.Subskill, entity.DifficultyBand, entity.ContextTagsJson, entity.FocusTagsJson);
                break;
            }
        }

        return (fixedIssues, providerName, modelName);
    }

    private static List<DiagnosticIssue> Diagnose(ResourceBankItem entity)
    {
        var issues = new List<DiagnosticIssue>();
        switch (entity.Type)
        {
            case PublishedResourceType.Vocabulary:
                var voc = ResourceBankItemContent.Deserialize<VocabularyContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(voc.Notes))
                    issues.Add(new DiagnosticIssue("missing_definition", "Missing a definition — this blocks multiple-choice Exercise generation.", true));
                break;
            case PublishedResourceType.Grammar:
                var gr = ResourceBankItemContent.Deserialize<GrammarContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(gr.Description))
                    issues.Add(new DiagnosticIssue("missing_description", "Missing a description.", true));
                break;
            case PublishedResourceType.ReadingReference:
                var rr = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(rr.ReferenceExcerpt))
                    issues.Add(new DiagnosticIssue("missing_excerpt", "Missing a reference excerpt.", true));
                break;
            case PublishedResourceType.ReadingPassage:
                var rp = ResourceBankItemContent.Deserialize<ReadingPassageContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(rp.Summary))
                    issues.Add(new DiagnosticIssue("missing_summary", "Missing a summary.", true));
                break;
            case PublishedResourceType.Writing:
                var w = ResourceBankItemContent.Deserialize<WritingPromptContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(w.PromptText))
                    issues.Add(new DiagnosticIssue("missing_prompt_text", "Missing prompt text.", true));
                break;
            case PublishedResourceType.Listening:
                var l = ResourceBankItemContent.Deserialize<ListeningPassageContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(l.Transcript))
                    issues.Add(new DiagnosticIssue("missing_transcript", "Missing a transcript.", true));
                break;
            case PublishedResourceType.Speaking:
                var sp = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(entity.ContentJson);
                if (string.IsNullOrWhiteSpace(sp.PromptText))
                    issues.Add(new DiagnosticIssue("missing_prompt_text", "Missing prompt text.", true));
                break;
        }
        return issues;
    }

    private static string TitleFor(ResourceBankItem entity)
    {
        try
        {
            return entity.Type switch
            {
                PublishedResourceType.Vocabulary => ResourceBankItemContent.Deserialize<VocabularyContent>(entity.ContentJson).Word,
                PublishedResourceType.Grammar => ResourceBankItemContent.Deserialize<GrammarContent>(entity.ContentJson).GrammarPoint,
                PublishedResourceType.ReadingReference => ResourceBankItemContent.Deserialize<ReadingReferenceContent>(entity.ContentJson).TextType ?? "Reading reference",
                PublishedResourceType.ReadingPassage => ResourceBankItemContent.Deserialize<ReadingPassageContent>(entity.ContentJson).Title,
                PublishedResourceType.Writing => ResourceBankItemContent.Deserialize<WritingPromptContent>(entity.ContentJson).Title,
                PublishedResourceType.Listening => ResourceBankItemContent.Deserialize<ListeningPassageContent>(entity.ContentJson).Title,
                PublishedResourceType.Speaking => ResourceBankItemContent.Deserialize<SpeakingPromptContent>(entity.ContentJson).Title,
                _ => entity.Id.ToString(),
            };
        }
        catch
        {
            return entity.Id.ToString();
        }
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
