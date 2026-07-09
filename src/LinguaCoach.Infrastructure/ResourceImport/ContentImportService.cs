using System.Text;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase H2 — Import Content UX v1. Thin admin-friendly wrapper over <see cref="IResourceImportService"/>:
/// finds-or-creates (and auto-approves) the named <see cref="CefrResourceSource"/>, converts the
/// admin's pasted content into the text shape <see cref="IResourceImportService.ImportAsync"/>
/// already parses, and forwards the admin's chosen resource type + default metadata as the
/// request's Phase H2 default fields. Deterministic only — "AI structure analysis" is explicitly
/// not implemented in this phase (see docs/architecture/product-model-realignment-h0.md); nothing
/// here guesses CEFR/tags beyond what a row's own columns or the admin's explicit defaults say.
/// </summary>
public sealed class ContentImportService : IContentImportService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IResourceImportService _importService;

    public ContentImportService(LinguaCoachDbContext db, IResourceImportService importService)
    {
        _db = db;
        _importService = importService;
    }

    public async Task<ContentImportResult> ImportContentAsync(ContentImportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceName))
            throw new ResourceImportValidationException("Source name is required.");
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ResourceImportValidationException("Content is required.");

        var sourceName = request.SourceName.Trim();
        var source = await _db.CefrResourceSources.FirstOrDefaultAsync(s => s.Name == sourceName, ct);
        if (source is null)
        {
            source = new CefrResourceSource(
                sourceName,
                licenseType: "AdminUpload",
                sourceUrl: null,
                usageRestrictionNotes: "Created automatically by the Import Content admin flow.",
                languageCode: CefrResourceSource.RequiredLanguageCode,
                allowsStudentDisplay: false,
                allowsCommercialUse: false);
            source.ApproveForImport("Auto-approved: admin-initiated Import Content run.");
            _db.CefrResourceSources.Add(source);
            await _db.SaveChangesAsync(ct);
        }
        else if (!source.IsImportApproved)
        {
            source.ApproveForImport("Auto-approved: admin-initiated Import Content run.");
            await _db.SaveChangesAsync(ct);
        }

        var (fileText, mode, fileName) = ConvertContent(request.InputMode, request.Content);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileText));

        var importRequest = new ResourceImportRequest(
            SourceId: source.Id,
            FileStream: stream,
            FileName: fileName,
            ImportMode: mode,
            ImportedByUserId: request.ImportedByUserId,
            Notes: request.Notes,
            DefaultCandidateType: request.ResourceType,
            DefaultCefrLevel: request.DefaultCefrLevel,
            DefaultSkill: request.DefaultSkill,
            DefaultSubskill: request.DefaultSubskill,
            DefaultContextTags: request.DefaultContextTags,
            DefaultFocusTags: request.DefaultFocusTags,
            DefaultDifficultyBand: request.DefaultDifficultyBand);

        var result = await _importService.ImportAsync(importRequest, ct);

        return new ContentImportResult(
            ImportRunId: result.RunId,
            SourceId: source.Id,
            RawRecordCount: result.TotalRecordCount,
            CandidateCount: result.SucceededCount,
            WarningCount: result.WarningCount,
            Status: result.Status,
            ErrorSummary: result.ErrorSummary,
            ReviewRoute: $"/admin/resource-candidates?importRunId={result.RunId}");
    }

    /// <summary>Converts pasted content into the (text, ImportMode, fileName) shape
    /// <see cref="IResourceImportService.ImportAsync"/> already parses. CSV/JSON text pass through
    /// unchanged. Pasted line-based text becomes one JSONL row per non-empty line, each staged
    /// under a generic <c>text</c> column — <see cref="ResourceImportService.ExtractCanonicalTextForType"/>
    /// (Phase H2) already knows how to read that column for every candidate type.</summary>
    private static (string FileText, ResourceImportMode Mode, string FileName) ConvertContent(
        ContentImportInputMode inputMode, string content)
    {
        switch (inputMode)
        {
            case ContentImportInputMode.CsvText:
                return (content, ResourceImportMode.Csv, "content-import.csv");
            case ContentImportInputMode.JsonText:
                return (content, ResourceImportMode.Json, "content-import.json");
            case ContentImportInputMode.PastedText:
                var lines = content
                    .Split('\n')
                    .Select(l => l.Trim('\r', ' ', '\t'))
                    .Where(l => l.Length > 0)
                    .Select(l => JsonSerializer.Serialize(new { text = l }));
                return (string.Join('\n', lines), ResourceImportMode.Jsonl, "content-import.jsonl");
            default:
                throw new ArgumentOutOfRangeException(nameof(inputMode), inputMode, "Unsupported content import input mode.");
        }
    }
}
