using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.4 (Workstream A7) — bounded, safe preview of a draft plan's structured-file mapping.
// Reads a handful of already-parsed sample rows per included, structured (CSV/JSON/JSONL) source
// group and runs them through IResourceImportService.PreviewRow — the exact column-rename +
// resource-type routing logic execution uses — without ever creating a ResourceRawRecord/
// ResourceCandidate/ResourceImportRun, and without any AI or STT call. Audio groups are not
// previewed here (see the interface doc comment). ──

internal sealed class ImportPlanPreviewService : IImportPlanPreviewService
{
    private static readonly HashSet<string> CsvExtensions = new(StringComparer.OrdinalIgnoreCase) { ".csv" };
    private static readonly HashSet<string> JsonExtensions = new(StringComparer.OrdinalIgnoreCase) { ".json" };
    private static readonly HashSet<string> JsonlExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jsonl" };

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IResourceImportService _resourceImportService;

    public ImportPlanPreviewService(LinguaCoachDbContext db, IFileStorageService storage, IResourceImportService resourceImportService)
    {
        _db = db;
        _storage = storage;
        _resourceImportService = resourceImportService;
    }

    public async Task<ImportPlanPreviewResult> PreviewAsync(PreviewImportPlanDraftCommand command, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");

        ImportPackageManifest? manifest = null;
        if (!string.IsNullOrEmpty(package.ManifestJson))
        {
            try { manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson); }
            catch (JsonException) { /* validation below tolerates a null manifest */ }
        }

        var validationErrors = ImportPlanInstructionValidator.Validate(command.GroupInstructions, manifest);

        var instructionsByGroup = command.GroupInstructions
            .Where(i => i.Included)
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var assets = await _db.ImportAssets
            .Where(a => a.ImportPackageId == package.Id && (
                CsvExtensions.Contains(a.FileExtension) || JsonExtensions.Contains(a.FileExtension) || JsonlExtensions.Contains(a.FileExtension)))
            .ToListAsync(ct);

        var rows = new List<ImportPlanPreviewRow>();
        foreach (var asset in assets)
        {
            ct.ThrowIfCancellationRequested();
            var groupKey = ImportExecutionGroupKey.ForRelativePath(asset.RelativePath);
            if (!instructionsByGroup.TryGetValue(groupKey, out var instruction)) continue;

            var warnings = new List<string>();
            List<IReadOnlyDictionary<string, string?>> sampleRows;
            try
            {
                await using var stream = await _storage.ReadAsync(asset.StorageKey, ct);
                using var reader = new StreamReader(stream);
                var fileText = await reader.ReadToEndAsync(ct);
                var mode = CsvExtensions.Contains(asset.FileExtension) ? ResourceImportMode.Csv
                    : JsonlExtensions.Contains(asset.FileExtension) ? ResourceImportMode.Jsonl
                    : ResourceImportMode.Json;
                var sample = _resourceImportService.ParseSample(fileText, mode, command.MaxSampleRowsPerGroup);
                sampleRows = sample.SampleRows.ToList();
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not parse this file for a preview: {ex.Message}");
                rows.Add(new ImportPlanPreviewRow(
                    groupKey, asset.RelativePath, new Dictionary<string, string?>(),
                    ResourceCandidateType.Unknown, "(unavailable)", warnings));
                continue;
            }

            foreach (var row in sampleRows)
            {
                var mapped = _resourceImportService.PreviewRow(row, instruction.FieldMappings, instruction.ResourceType);
                rows.Add(new ImportPlanPreviewRow(
                    groupKey, asset.RelativePath, row, mapped.CandidateType, mapped.CanonicalText, Array.Empty<string>()));
            }
        }

        return new ImportPlanPreviewResult(rows, validationErrors);
    }
}
