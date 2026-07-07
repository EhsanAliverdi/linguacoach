using System.Text.Json;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

/// <summary>
/// Parses an <c>ActivityTemplate.ValidationRulesJson</c> blob and validates a candidate
/// AI-generated instance schema against it. Expected shape (all fields optional):
/// <code>{ "requiredComponentKeys": ["prompt_text"], "maxSchemaLength": 4000, "forbiddenWords": ["lorem"] }</code>
/// Malformed or absent ValidationRulesJson means no additional constraints — the shared
/// Form.io student-safe schema check (<c>IFormIoSchemaValidationService</c>) still applies
/// independently of this class.
/// </summary>
public sealed record ActivityTemplateValidationRules(
    IReadOnlyList<string> RequiredComponentKeys,
    int? MaxSchemaLength,
    IReadOnlyList<string> ForbiddenWords)
{
    public static ActivityTemplateValidationRules Parse(string? validationRulesJson)
    {
        if (string.IsNullOrWhiteSpace(validationRulesJson))
            return new ActivityTemplateValidationRules([], null, []);

        try
        {
            using var doc = JsonDocument.Parse(validationRulesJson);
            var root = doc.RootElement;

            var requiredKeys = ReadStringArray(root, "requiredComponentKeys");
            var forbiddenWords = ReadStringArray(root, "forbiddenWords");
            int? maxLength = root.TryGetProperty("maxSchemaLength", out var maxLenEl)
                && maxLenEl.ValueKind == JsonValueKind.Number
                ? maxLenEl.GetInt32()
                : null;

            return new ActivityTemplateValidationRules(requiredKeys, maxLength, forbiddenWords);
        }
        catch (JsonException)
        {
            return new ActivityTemplateValidationRules([], null, []);
        }
    }

    /// <summary>Validates a candidate Form.io schema JSON against these rules. Returns an empty
    /// list if there are no violations.</summary>
    public IReadOnlyList<string> Validate(string candidateSchemaJson)
    {
        var errors = new List<string>();

        if (MaxSchemaLength is { } maxLength && candidateSchemaJson.Length > maxLength)
            errors.Add($"Generated schema length {candidateSchemaJson.Length} exceeds maxSchemaLength {maxLength}.");

        if (ForbiddenWords.Count > 0)
        {
            foreach (var word in ForbiddenWords)
            {
                if (candidateSchemaJson.Contains(word, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Generated schema contains forbidden word '{word}'.");
            }
        }

        if (RequiredComponentKeys.Count > 0)
        {
            HashSet<string> presentKeys;
            try
            {
                using var doc = JsonDocument.Parse(candidateSchemaJson);
                presentKeys = CollectComponentKeys(doc.RootElement);
            }
            catch (JsonException)
            {
                errors.Add("Generated schema is not valid JSON — cannot check required component keys.");
                return errors;
            }

            foreach (var requiredKey in RequiredComponentKeys)
            {
                if (!presentKeys.Contains(requiredKey))
                    errors.Add($"Generated schema is missing required component key '{requiredKey}'.");
            }
        }

        return errors;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }

    private static HashSet<string> CollectComponentKeys(JsonElement root)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = root.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array
            ? comps
            : root.ValueKind == JsonValueKind.Array ? root : default;

        if (components.ValueKind == JsonValueKind.Array)
            Walk(components, keys);

        return keys;
    }

    private static void Walk(JsonElement componentsArray, HashSet<string> keys)
    {
        foreach (var component in componentsArray.EnumerateArray())
        {
            if (component.ValueKind != JsonValueKind.Object) continue;

            if (component.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String)
            {
                var key = keyEl.GetString();
                if (!string.IsNullOrWhiteSpace(key)) keys.Add(key!);
            }

            if (component.TryGetProperty("components", out var childComps) && childComps.ValueKind == JsonValueKind.Array)
                Walk(childComps, keys);

            foreach (var arrProp in new[] { "columns", "rows" })
            {
                if (!component.TryGetProperty(arrProp, out var arrEl) || arrEl.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var cell in arrEl.EnumerateArray())
                {
                    if (cell.ValueKind == JsonValueKind.Object && cell.TryGetProperty("components", out var cellComps)
                        && cellComps.ValueKind == JsonValueKind.Array)
                        Walk(cellComps, keys);
                }
            }
        }
    }
}
