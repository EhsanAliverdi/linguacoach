using System.Text.Json;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Shared helper for reading a staged <see cref="LinguaCoach.Domain.Entities.ResourceCandidate"/>'s
/// field-name-keyed <c>NormalizedJson</c> payload. Factored out of
/// <see cref="ResourceCandidatePreviewService"/> (Phase E3) so Phase E4's
/// <see cref="ResourceCandidatePublishService"/> can reuse the exact same "word"/"lemma",
/// "grammarKey"/"title", "passage"/"text" field-name lookup rather than a third, subtly-different
/// implementation — mirrors <see cref="ResourceCandidateFormIoHelper"/>'s existing precedent for
/// the Form.io schema lookup.
/// </summary>
internal static class ResourceCandidateFieldHelper
{
    /// <summary>Parses NormalizedJson's field-keyed row into a plain dictionary, full-fidelity
    /// (no truncation) — safe for internal use (e.g. accurate word counts, exact field values to
    /// map onto a bank entity constructor), never surfaced directly on a student/admin-facing DTO
    /// in this un-truncated form.</summary>
    public static Dictionary<string, string?> ParseFields(string normalizedJson)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(normalizedJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => prop.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
            // Unparsable NormalizedJson — return whatever was collected (empty dict) rather than
            // throwing; callers treat a missing field the same way as an unparsable payload.
        }
        return result;
    }

    public static string? GetFieldCI(IReadOnlyDictionary<string, string?> dict, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            foreach (var kv in dict)
            {
                if (string.Equals(kv.Key, fieldName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kv.Value))
                {
                    return kv.Value;
                }
            }
        }
        return null;
    }
}
