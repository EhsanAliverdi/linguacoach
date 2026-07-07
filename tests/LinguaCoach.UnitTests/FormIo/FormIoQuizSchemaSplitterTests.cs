using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Infrastructure.FormIo;

namespace LinguaCoach.UnitTests.FormIo;

public sealed class FormIoQuizSchemaSplitterTests
{
    private readonly FormIoQuizSchemaSplitter _splitter = new();

    [Fact]
    public void Split_ExtractsEnabledQuizIntoScoringRules_AndStripsQuizFromStudentSchema()
    {
        var authoring = """
        {
          "components": [
            {
              "type": "radio",
              "key": "answer",
              "label": "Pick one",
              "quiz": { "enabled": true, "rule": { "kind": "single_choice", "correctAnswer": "B", "points": 2.0 } }
            }
          ]
        }
        """;

        var result = _splitter.Split(authoring);

        using var studentDoc = JsonDocument.Parse(result.StudentSchemaJson);
        var component = studentDoc.RootElement.GetProperty("components")[0];
        Assert.False(component.TryGetProperty("quiz", out _));
        Assert.Equal("answer", component.GetProperty("key").GetString());

        var scoringDoc = JsonSerializer.Deserialize<ScoringRulesDocument>(result.ScoringRulesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var rule = scoringDoc.Components["answer"];
        Assert.Equal("single_choice", rule.Kind);
        Assert.Equal("B", rule.CorrectAnswer);
        Assert.Equal(2.0, rule.Points);
    }

    [Fact]
    public void Split_DisabledQuiz_StripsFromSchemaButDoesNotEmitScoringRule()
    {
        var authoring = """
        {
          "components": [
            { "type": "textfield", "key": "answer", "quiz": { "enabled": false, "rule": { "kind": "text_exact", "correctAnswer": "x" } } }
          ]
        }
        """;

        var result = _splitter.Split(authoring);

        using var studentDoc = JsonDocument.Parse(result.StudentSchemaJson);
        Assert.False(studentDoc.RootElement.GetProperty("components")[0].TryGetProperty("quiz", out _));

        var scoringDoc = JsonSerializer.Deserialize<ScoringRulesDocument>(result.ScoringRulesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Empty(scoringDoc.Components);
    }

    [Theory]
    [InlineData("""{ "type": "textfield", "key": "answer", "quiz": "not-an-object" }""")]
    [InlineData("""{ "type": "textfield", "key": "answer", "quiz": { "enabled": "yes-please" } }""")]
    [InlineData("""{ "type": "textfield", "key": "answer", "quiz": { "enabled": true } }""")]
    [InlineData("""{ "type": "textfield", "key": "answer", "quiz": { "enabled": true, "rule": "garbage" } }""")]
    [InlineData("""{ "type": "textfield", "key": "answer", "quiz": { "enabled": true, "rule": { "kind": 12345 } } }""")]
    [InlineData("""{ "type": "textfield", "key": "answer", "quiz": null }""")]
    [InlineData("""{ "type": "textfield", "quiz": { "enabled": true, "rule": { "kind": "text_exact", "correctAnswer": "x" } } }""")]
    public void Split_MalformedQuizAnnotation_NeverThrows_AndStillStripsQuizKey(string componentJson)
    {
        var authoring = $$"""{ "components": [ {{componentJson}} ] }""";

        var result = _splitter.Split(authoring);

        using var studentDoc = JsonDocument.Parse(result.StudentSchemaJson);
        Assert.False(studentDoc.RootElement.GetProperty("components")[0].TryGetProperty("quiz", out _));
    }

    [Fact]
    public void Split_NeverLeavesQuizKeyAnywhereInOutputSchema_ForArbitrarilyNestedInput()
    {
        // "quiz" nested at every legal container depth: panel -> columns -> table cell -> wizard page.
        var authoring = """
        {
          "display": "wizard",
          "components": [
            {
              "type": "panel", "key": "page1", "components": [
                { "type": "radio", "key": "q1", "quiz": { "enabled": true, "rule": { "kind": "single_choice", "correctAnswer": "A" } } },
                {
                  "type": "columns", "key": "cols1", "columns": [
                    { "components": [
                      { "type": "textfield", "key": "q2", "quiz": { "enabled": true, "rule": { "kind": "text_normalized", "correctAnswer": "hi" } } }
                    ] },
                    { "components": [
                      { "type": "textarea", "key": "q3", "quiz": { "enabled": false } }
                    ] }
                  ]
                },
                {
                  "type": "table", "key": "table1", "rows": [
                    { "components": [
                      { "type": "selectboxes", "key": "q4", "quiz": { "enabled": true, "rule": { "kind": "multiple_choice", "correctAnswers": ["a", "b"] } } }
                    ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var result = _splitter.Split(authoring);

        // Walk the entire output JSON tree — not just known keys — asserting no node is named
        // "quiz" (case-insensitive) anywhere, and the raw string never contains the answer keys.
        using var doc = JsonDocument.Parse(result.StudentSchemaJson);
        AssertNoPropertyNamed(doc.RootElement, "quiz");

        Assert.DoesNotContain("\"quiz\"", result.StudentSchemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", result.StudentSchemaJson, StringComparison.OrdinalIgnoreCase);

        var scoringDoc = JsonSerializer.Deserialize<ScoringRulesDocument>(result.ScoringRulesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(3, scoringDoc.Components.Count);
        Assert.Equal("A", scoringDoc.Components["q1"].CorrectAnswer);
        Assert.Equal("hi", scoringDoc.Components["q2"].CorrectAnswer);
        Assert.Equal(new[] { "a", "b" }, scoringDoc.Components["q4"].CorrectAnswers);
        Assert.False(scoringDoc.Components.ContainsKey("q3")); // disabled — never emitted
    }

    private static void AssertNoPropertyNamed(JsonElement element, string forbiddenName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    Assert.False(string.Equals(prop.Name, forbiddenName, StringComparison.OrdinalIgnoreCase),
                        $"Found forbidden property '{prop.Name}' in output schema.");
                    AssertNoPropertyNamed(prop.Value, forbiddenName);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AssertNoPropertyNamed(item, forbiddenName);
                break;
        }
    }
}
