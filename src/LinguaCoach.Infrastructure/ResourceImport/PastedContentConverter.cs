using System.Text.Json;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.2 — extracted from the retired Phase H2 ContentImportService: converts admin-pasted,
/// line-based text into the same one-JSON-object-per-line shape ResourceImportService's Jsonl
/// mode already parses (each non-empty line becomes <c>{"text": line}</c>, staged under a generic
/// "text" column that ResourceImportService.ExtractCanonicalTextForType already knows how to read
/// for every candidate type). This phase deliberately narrows Phase H2's original three paste
/// modes (pasted_text/csv_text/json_text) down to line-based-only — pasting raw CSV/JSON text into
/// the same box is no longer separately supported; upload a CSV/JSON file instead. See the Phase
/// 4.2 review doc for this scope decision.
/// </summary>
internal static class PastedContentConverter
{
    public static string ToJsonLines(string content)
    {
        var lines = content
            .Split('\n')
            .Select(l => l.Trim('\r', ' ', '\t'))
            .Where(l => l.Length > 0)
            .Select(l => JsonSerializer.Serialize(new { text = l }));
        return string.Join('\n', lines);
    }
}
