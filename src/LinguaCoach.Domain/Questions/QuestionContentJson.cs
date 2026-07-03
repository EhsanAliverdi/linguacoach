using System.Text.Json;
using System.Text.Json.Nodes;

namespace LinguaCoach.Domain.Questions;

/// <summary>
/// Safe (de)serialization for ContentJson columns. Deserialization failures are swallowed and
/// return null rather than throwing — every caller of a Content/Answer getter already has a
/// fallback (derive from the legacy flat fields, or treat as "not yet answered"), so one
/// unexpectedly-shaped row must never take down an entire list/read endpoint for every row.
/// </summary>
public static class QuestionContentJson
{
    // Case-insensitive: QuestionAnswer JSON submitted by the frontend is camelCase
    // (questionId/values), matching the TS models — a plain JsonSerializer.Deserialize call
    // (unlike ASP.NET's MVC model binding, which applies camelCase policy automatically) is
    // case-sensitive by default and would silently fail to bind onto the PascalCase C#
    // properties, discarding every submitted answer.
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static QuestionContent? TryDeserializeContent(string? json)
    {
        if (json is null) return null;
        try
        {
            // Postgres jsonb columns do not preserve object key insertion order (keys are
            // reordered internally, e.g. shorter names first), but System.Text.Json's built-in
            // polymorphic deserializer requires the "type" discriminator to be the first property
            // in every JSON object. ContentJson round-tripped through jsonb storage routinely
            // arrives with "type" after other properties (e.g. "Id"), which throws
            // NotSupportedException even though the JSON is perfectly well-formed. Normalizing
            // "type" back to the front, recursively (including nested group sub-questions), before
            // handing off to the built-in polymorphic reader fixes this regardless of storage order.
            var normalized = MoveTypeDiscriminatorFirst(json);
            return JsonSerializer.Deserialize<QuestionContent>(normalized, Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string MoveTypeDiscriminatorFirst(string json)
    {
        var node = JsonNode.Parse(json);
        return Reorder(node)?.ToJsonString() ?? json;
    }

    private static JsonNode? Reorder(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject();
                if (obj.TryGetPropertyValue("type", out var typeValue))
                    result["type"] = Reorder(typeValue);
                foreach (var property in obj)
                {
                    if (property.Key == "type") continue;
                    result[property.Key] = Reorder(property.Value);
                }
                return result;
            case JsonArray arr:
                var arrResult = new JsonArray();
                foreach (var item in arr) arrResult.Add(Reorder(item));
                return arrResult;
            default:
                return node?.DeepClone();
        }
    }

    public static QuestionAnswer? TryDeserializeAnswer(string? json)
    {
        if (json is null) return null;
        try
        {
            return Normalize(JsonSerializer.Deserialize<QuestionAnswer>(json, Options));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Always returns a non-null QuestionAnswer with a non-null Answers list — for callers
    /// (e.g. onboarding's step submission) that need "no answer/skip" to behave as an empty answer
    /// rather than null. A JSON object with no "answers" key (e.g. "{}", used for skippable steps)
    /// deserializes Answers as null, not [], since System.Text.Json doesn't default missing
    /// reference-type record parameters to empty collections.</summary>
    public static QuestionAnswer TryDeserializeAnswerOrEmpty(string? json) =>
        TryDeserializeAnswer(json) ?? new QuestionAnswer([]);

    private static QuestionAnswer? Normalize(QuestionAnswer? answer) =>
        answer is null ? null : new QuestionAnswer(answer.Answers ?? []);
}
