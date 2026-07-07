using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.Activity;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// See <see cref="IActivityContentFingerprintService"/>.
///
/// Normalization strategy (documented per Phase B scope — deterministic/exact-match only, no
/// embeddings, no semantic near-duplicate detection):
///  1. Parse ContentJson (if present and valid) into a JsonElement tree.
///  2. Recursively strip a small, fixed list of volatile/non-content property names (see
///     <see cref="VolatileKeys"/>) — defensive only; the known shapes (ModuleStageSchema,
///     Form.io component trees) do not normally embed IDs/timestamps inside content JSON.
///  3. Recursively sort object properties by key and re-serialize compactly, so property
///     ordering never affects the fingerprint.
///  4. Trim and lowercase all string leaf values, so whitespace/casing differences don't
///     produce different fingerprints for otherwise-identical content.
///  5. Concatenate the normalized content with the non-null metadata inputs (pattern key,
///     skill/subskill/CEFR, topic/scenario/passage/prompt keys) and SHA-256 hash the result.
///
/// Limitation: if ContentJson is null/blank/invalid JSON, or ContentShape is Unknown, this
/// falls back to hashing whatever normalized text is available (metadata only, or the raw
/// trimmed/lowercased string if it isn't valid JSON) — content-level dedup degrades to
/// metadata-level dedup in that case. This is a known, accepted limitation for this phase.
/// </summary>
public sealed class ActivityContentFingerprintService : IActivityContentFingerprintService
{
    // Defensive strip list — property names (case-insensitive) that are never part of the
    // content itself, only of how/when it was produced or stored.
    private static readonly HashSet<string> VolatileKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "_id", "activityId", "learningActivityId", "sessionId", "attemptId",
        "studentId", "studentProfileId", "createdAt", "createdAtUtc", "updatedAt",
        "updatedAtUtc", "timestamp", "generatedAt", "generatedAtUtc", "correlationId",
        "requestId", "generatedByModel", "generatedByProvider"
    };

    public string ComputeFingerprint(ActivityContentFingerprintRequest request)
    {
        var normalizedContent = NormalizeContent(request.ContentJson, request.ContentShape);

        var parts = new[]
        {
            normalizedContent,
            NormalizePart(request.PatternKey),
            NormalizePart(request.Skill),
            NormalizePart(request.Subskill),
            NormalizePart(request.CefrLevel),
            NormalizePart(request.TopicKey),
            NormalizePart(request.ScenarioKey),
            NormalizePart(request.PassageKey),
            NormalizePart(request.PromptKey),
        };

        var canonical = string.Join('|', parts);
        return Sha256Hex(canonical);
    }

    private static string NormalizeContent(string? contentJson, ActivityContentShape shape)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
            return string.Empty;

        // ContentShape doesn't currently change how normalization happens — both known shapes
        // (ModuleStageSchema, FormIoSchema) benefit from the same generic sort+strip+lowercase
        // pass, since neither embeds shape-specific volatile data beyond the common list above.
        // Kept as a distinct parameter (rather than removed) so shape-specific normalization can
        // be added later without changing the public contract — see Unknown-shape fallback below.
        _ = shape;

        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var normalized = NormalizeElement(doc.RootElement);
            return JsonSerializer.Serialize(normalized);
        }
        catch (JsonException)
        {
            // Not valid JSON — fall back to a trimmed/lowercased plain-text hash input rather
            // than throwing. Documented limitation: this degrades to a text-level fingerprint.
            return contentJson.Trim().ToLowerInvariant();
        }
    }

    private static object? NormalizeElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var prop in element.EnumerateObject())
                {
                    if (VolatileKeys.Contains(prop.Name)) continue;
                    sorted[prop.Name.ToLowerInvariant()] = NormalizeElement(prop.Value);
                }
                return sorted;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                    list.Add(NormalizeElement(item));
                return list;

            case JsonValueKind.String:
                return CollapseWhitespace(element.GetString() ?? string.Empty).ToLowerInvariant();

            case JsonValueKind.Number:
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static string CollapseWhitespace(string value)
    {
        var trimmed = value.Trim();
        var sb = new StringBuilder(trimmed.Length);
        var lastWasSpace = false;
        foreach (var c in trimmed)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    private static string NormalizePart(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : CollapseWhitespace(value).ToLowerInvariant();

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
