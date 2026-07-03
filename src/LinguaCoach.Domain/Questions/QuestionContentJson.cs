using System.Text.Json;

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
            return JsonSerializer.Deserialize<QuestionContent>(json, Options);
        }
        catch (JsonException)
        {
            return null;
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
