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
    public static QuestionContent? TryDeserializeContent(string? json)
    {
        if (json is null) return null;
        try
        {
            return JsonSerializer.Deserialize<QuestionContent>(json);
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
            return JsonSerializer.Deserialize<QuestionAnswer>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
