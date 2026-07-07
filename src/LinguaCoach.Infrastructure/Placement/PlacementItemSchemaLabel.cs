using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>Derives display-only metadata from a Form.io schema — the schema is the only
/// authored source of truth for an item's question/type now that ItemType/Prompt have been
/// removed from PlacementItemDefinition. Used for the admin list preview column and for the
/// label/type snapshot still carried on issued PlacementAssessmentItem rows.</summary>
internal static class PlacementItemSchemaLabel
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The first component's label (falling back to its key), or "(no question)" when
    /// the schema has no components yet.</summary>
    public static string ExtractLabel(string? formIoSchemaJson)
    {
        var component = FirstComponent(formIoSchemaJson);
        if (component is null) return "(no question)";

        if (component.Value.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(labelEl.GetString()))
        {
            return labelEl.GetString()!;
        }

        return component.Value.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String
            ? keyEl.GetString()!
            : "(no question)";
    }

    /// <summary>The first component's Form.io "type" (e.g. "radio", "textfield"), or "unknown".</summary>
    public static string ExtractComponentType(string? formIoSchemaJson)
    {
        var component = FirstComponent(formIoSchemaJson);
        if (component is null) return "unknown";

        return component.Value.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
            ? typeEl.GetString()!
            : "unknown";
    }

    private static JsonElement? FirstComponent(string? formIoSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(formIoSchemaJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(formIoSchemaJson);
            if (doc.RootElement.TryGetProperty("components", out var componentsEl)
                && componentsEl.ValueKind == JsonValueKind.Array
                && componentsEl.GetArrayLength() > 0)
            {
                return componentsEl[0].Clone();
            }
        }
        catch (JsonException)
        {
            // Malformed schema — treated the same as "no components" for display purposes.
        }

        return null;
    }

    /// <summary>Stable identity for "is this the same item" — used to dedupe seed rows and to
    /// reject an admin re-adding an identical item, since Prompt no longer exists to serve that
    /// role. Two items with the same skill/level and byte-identical schema are the same item.</summary>
    public static string ComputeIdentityHash(string skill, string cefrLevel, string? formIoSchemaJson)
    {
        var input = $"{skill}|{cefrLevel}|{formIoSchemaJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
