using System.Text.Json;
using LinguaCoach.Application.Placement;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>Shared validation for admin item-bank authoring: the ScoringRulesJson document must
/// deserialize, and every component key it references must exist in the paired Form.io schema —
/// an orphaned scoring key silently never scores anything, so it's rejected at write time.</summary>
internal static class PlacementFormIoScoringValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ScoringRulesDocument ValidateAndParse(string formIoSchemaJson, string scoringRulesJson)
    {
        ScoringRulesDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<ScoringRulesDocument>(scoringRulesJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new PlacementItemValidationException($"Scoring rules JSON is invalid: {ex.Message}");
        }

        if (doc is null || doc.Components.Count == 0)
            throw new PlacementItemValidationException("Scoring rules must declare at least one component.");

        var schemaKeys = ExtractComponentKeys(formIoSchemaJson);
        var orphaned = doc.Components.Keys.Where(k => !schemaKeys.Contains(k)).ToList();
        if (orphaned.Count > 0)
        {
            throw new PlacementItemValidationException(
                $"Scoring rules reference component key(s) not present in the Form.io schema: {string.Join(", ", orphaned)}");
        }

        return doc;
    }

    private static readonly HashSet<string> NonAnswerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "button", "content", "panel", "columns", "table", "wizard", "form"
    };

    private static HashSet<string> ExtractComponentKeys(string formIoSchemaJson)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(formIoSchemaJson);
        }
        catch (JsonException ex)
        {
            throw new PlacementItemValidationException($"Form.io schema JSON is invalid: {ex.Message}");
        }

        using (doc)
        {
            Walk(doc.RootElement, keys);
        }

        return keys;
    }

    private static void Walk(JsonElement el, HashSet<string> keys)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                if (el.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                    && !NonAnswerTypes.Contains(typeEl.GetString()!)
                    && el.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String)
                {
                    keys.Add(keyEl.GetString()!);
                }

                foreach (var prop in el.EnumerateObject())
                    Walk(prop.Value, keys);
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    Walk(item, keys);
                break;
        }
    }
}
