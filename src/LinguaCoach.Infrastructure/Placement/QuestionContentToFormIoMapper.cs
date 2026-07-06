using System.Text.Json;
using System.Text.Json.Nodes;
using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>Server-side fallback mapper from the shared QuestionContent schema to a Form.io
/// component schema, used to render a placement item that hasn't been re-authored with a native
/// FormIoSchemaJson yet. Always called against an already-redacted QuestionContent
/// (QuestionContentRedactor.RedactCorrectAnswers), so it can never leak a correct answer.</summary>
public static class QuestionContentToFormIoMapper
{
    public static string Map(QuestionContent content)
    {
        var components = new JsonArray { MapComponent(content) };
        var root = new JsonObject { ["components"] = components };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject MapComponent(QuestionContent content) => content switch
    {
        SingleChoiceQuestion q => new JsonObject
        {
            ["type"] = "radio",
            ["key"] = q.Id,
            ["label"] = q.QuestionText,
            ["values"] = new JsonArray(q.Choices.Select(c => (JsonNode)new JsonObject
            {
                ["label"] = c.Label,
                ["value"] = c.Key
            }).ToArray())
        },
        MultipleChoiceQuestion q => new JsonObject
        {
            ["type"] = "selectboxes",
            ["key"] = q.Id,
            ["label"] = q.QuestionText,
            ["values"] = new JsonArray(q.Choices.Select(c => (JsonNode)new JsonObject
            {
                ["label"] = c.Label,
                ["value"] = c.Key
            }).ToArray())
        },
        GapFillQuestion q => new JsonObject
        {
            ["type"] = "textfield",
            ["key"] = q.Id,
            ["label"] = q.QuestionText
        },
        FreeTextQuestion q => new JsonObject
        {
            ["type"] = q.IsMultiline ? "textarea" : "textfield",
            ["key"] = q.Id,
            ["label"] = q.QuestionText,
            ["placeholder"] = q.Placeholder,
            ["validate"] = new JsonObject { ["maxLength"] = q.MaxLength }
        },
        ListeningGroupQuestion q => new JsonObject
        {
            ["type"] = "panel",
            ["key"] = $"{q.Id}_panel",
            ["title"] = "Listening",
            ["components"] = new JsonArray(
                new[] { (JsonNode)new JsonObject { ["type"] = "content", ["key"] = $"{q.Id}_instructions", ["html"] = q.Instructions ?? string.Empty } }
                    .Concat(q.Questions.Select(sub => (JsonNode)MapComponent(sub)))
                    .ToArray())
        },
        ReadingGroupQuestion q => new JsonObject
        {
            ["type"] = "panel",
            ["key"] = $"{q.Id}_panel",
            ["title"] = "Reading",
            ["components"] = new JsonArray(
                new[] { (JsonNode)new JsonObject { ["type"] = "content", ["key"] = $"{q.Id}_passage", ["html"] = q.Passage } }
                    .Concat(q.Questions.Select(sub => (JsonNode)MapComponent(sub)))
                    .ToArray())
        },
        _ => new JsonObject { ["type"] = "content", ["key"] = content.Id, ["html"] = string.Empty }
    };
}
