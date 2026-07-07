using System.Text.Json;
using LinguaCoach.Application.FormIo;

namespace LinguaCoach.UnitTests.FormIo;

/// <summary>Covers FormIoQuizAnnotationCodec.Embed — the reverse of Split, used to backfill
/// quiz annotations onto items/versions that had scoring before the Quiz tab existed.</summary>
public sealed class FormIoQuizAnnotationCodecEmbedTests
{
    [Fact]
    public void Embed_UnifiedScoringRulesShape_AddsQuizAnnotationMatchedByKey()
    {
        var schema = """{"components":[{"type":"radio","key":"answer","label":"Pick"}]}""";
        var scoringRules = """{"components":{"answer":{"kind":"single_choice","correctAnswer":"B","points":1}}}""";

        var embedded = FormIoQuizAnnotationCodec.Embed(schema, scoringRules);

        using var doc = JsonDocument.Parse(embedded);
        var component = doc.RootElement.GetProperty("components")[0];
        var quiz = component.GetProperty("quiz");
        Assert.True(quiz.GetProperty("enabled").GetBoolean());
        Assert.Equal("single_choice", quiz.GetProperty("rule").GetProperty("kind").GetString());
        Assert.Equal("B", quiz.GetProperty("rule").GetProperty("correctAnswer").GetString());
    }

    [Fact]
    public void Embed_LegacyFlatScoringRulesShape_StillAddsQuizAnnotation()
    {
        var schema = """{"components":[{"type":"radio","key":"assessment_q1","label":"Pick"}]}""";
        var legacyScoringRules = """{"assessment_q1":{"correctAnswerKey":"b"}}""";

        var embedded = FormIoQuizAnnotationCodec.Embed(schema, legacyScoringRules);

        using var doc = JsonDocument.Parse(embedded);
        var quiz = doc.RootElement.GetProperty("components")[0].GetProperty("quiz");
        Assert.True(quiz.GetProperty("enabled").GetBoolean());
        Assert.Equal("single_choice", quiz.GetProperty("rule").GetProperty("kind").GetString());
        Assert.Equal("b", quiz.GetProperty("rule").GetProperty("correctAnswer").GetString());
    }

    [Fact]
    public void Embed_ComponentKeyNotInScoringRules_LeftUntouched()
    {
        var schema = """{"components":[{"type":"radio","key":"unscored","label":"Pick"}]}""";
        var scoringRules = """{"components":{"answer":{"kind":"single_choice","correctAnswer":"B"}}}""";

        var embedded = FormIoQuizAnnotationCodec.Embed(schema, scoringRules);

        using var doc = JsonDocument.Parse(embedded);
        Assert.False(doc.RootElement.GetProperty("components")[0].TryGetProperty("quiz", out _));
    }

    [Fact]
    public void Embed_NullOrEmptyScoringRules_ReturnsSchemaUnchanged()
    {
        var schema = """{"components":[{"type":"radio","key":"answer","label":"Pick"}]}""";

        Assert.Equal(schema, FormIoQuizAnnotationCodec.Embed(schema, null));
        Assert.Equal(schema, FormIoQuizAnnotationCodec.Embed(schema, ""));
    }

    [Fact]
    public void Embed_NestedContainers_FindsComponentsAtEveryDepth()
    {
        var schema = """
        {
          "components": [
            { "type": "panel", "key": "page1", "components": [
              { "type": "radio", "key": "q1", "label": "Q1" }
            ] }
          ]
        }
        """;
        var scoringRules = """{"components":{"q1":{"kind":"single_choice","correctAnswer":"A"}}}""";

        var embedded = FormIoQuizAnnotationCodec.Embed(schema, scoringRules);

        using var doc = JsonDocument.Parse(embedded);
        var nested = doc.RootElement.GetProperty("components")[0].GetProperty("components")[0];
        Assert.True(nested.GetProperty("quiz").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void SplitThenEmbed_RoundTripsToTheSameQuizAnnotation()
    {
        var authoring = """
        {"components":[{"type":"textfield","key":"answer","label":"Q","quiz":{"enabled":true,"rule":{"kind":"text_normalized","correctAnswer":"hi","points":1}}}]}
        """;

        var split = FormIoQuizAnnotationCodec.Split(authoring);
        var reEmbedded = FormIoQuizAnnotationCodec.Embed(split.StudentSchemaJson, split.ScoringRulesJson);

        using var doc = JsonDocument.Parse(reEmbedded);
        var rule = doc.RootElement.GetProperty("components")[0].GetProperty("quiz").GetProperty("rule");
        Assert.Equal("text_normalized", rule.GetProperty("kind").GetString());
        Assert.Equal("hi", rule.GetProperty("correctAnswer").GetString());
    }
}
