using System.Text.Json;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Shared helper for locating the Form.io-shaped schema JSON staged inside an
/// ActivityTemplateCandidate's <c>NormalizedJson</c> (under whichever of 'formio'/'schema'/
/// 'template' was populated on the as-imported row — see
/// <see cref="LinguaCoach.Infrastructure.ResourceImport.ResourceImportService"/>'s field-name
/// conventions). Factored out of <see cref="ResourceCandidateValidationService"/> (Phase E2) so
/// Phase E3's preview service can reuse the exact same lookup logic rather than a second,
/// subtly-different implementation.
/// </summary>
internal static class ResourceCandidateFormIoHelper
{
    public static string? ExtractFormIoSchemaJson(string normalizedJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(normalizedJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var fieldName in new[] { "formio", "schema", "template" })
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                    {
                        return prop.Value.GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not parseable — callers treat a null return as "no recognizable schema" rather
            // than throwing.
        }

        return null;
    }
}
