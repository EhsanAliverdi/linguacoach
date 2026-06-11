using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Vocabulary;

namespace LinguaCoach.UnitTests.Vocabulary;

/// <summary>
/// Vocabulary extraction must work from any activity that produces AI feedback —
/// not only legacy WritingScenario attempts. Pattern-evaluated activities (email reply,
/// workplace chat, etc.) serialize feedback as PatternEvaluationResult ("corrections"),
/// while legacy writing feedback uses "changes". BuildExtractionContext must normalise
/// both shapes into the same extraction prompt input.
/// </summary>
public sealed class VocabularyExtractionContextTests
{
    private static StudentProfile NewProfile() => new(Guid.NewGuid());

    [Fact]
    public void BuildExtractionContext_WithLegacyChangesShape_ExtractsChanges()
    {
        const string feedbackJson = """
            {
              "coachSummary": "Good effort.",
              "changes": [
                { "type": "replace", "original": "please send", "suggested": "Could you please send", "category": "tone", "severity": "high" }
              ]
            }
            """;

        var json = VocabularyExtractionService.BuildExtractionContext(
            NewProfile(), activity: null, module: null,
            submittedContent: "please send the file",
            feedbackJson: feedbackJson,
            improvedVersion: null,
            knownTerms: []);

        using var doc = JsonDocument.Parse(json);
        var changes = doc.RootElement.GetProperty("feedbackChanges");
        changes.GetArrayLength().Should().Be(1);
        changes[0].GetProperty("suggested").GetString().Should().Be("Could you please send");
        changes[0].GetProperty("category").GetString().Should().Be("tone");
    }

    [Fact]
    public void BuildExtractionContext_WithPatternEvaluationCorrectionsShape_ExtractsCorrections()
    {
        // Shape produced by PatternEvaluationResult serialization (camelCase "corrections")
        // for AiStructured/AiOpenEnded patterns such as email_reply and teams_chat_simulation.
        const string feedbackJson = """
            {
              "coachSummary": "Clear reply, but tone could be softer.",
              "corrections": [
                { "category": "tone", "original": "send me the file now", "suggestion": "Could you please send me the file when you have a chance?", "explanation": "Softer requests are more professional." }
              ]
            }
            """;

        var json = VocabularyExtractionService.BuildExtractionContext(
            NewProfile(), activity: null, module: null,
            submittedContent: "send me the file now",
            feedbackJson: feedbackJson,
            improvedVersion: "Could you please send me the file when you have a chance?",
            knownTerms: []);

        using var doc = JsonDocument.Parse(json);
        var changes = doc.RootElement.GetProperty("feedbackChanges");
        changes.GetArrayLength().Should().Be(1);
        changes[0].GetProperty("suggested").GetString()
            .Should().Be("Could you please send me the file when you have a chance?");
        changes[0].GetProperty("category").GetString().Should().Be("tone");
        doc.RootElement.GetProperty("improvedVersion").GetString()
            .Should().Be("Could you please send me the file when you have a chance?");
    }

    [Fact]
    public void BuildExtractionContext_WithNoChangesOrCorrections_ProducesEmptyFeedbackChanges()
    {
        const string feedbackJson = """{ "coachSummary": "Activity completed." }""";

        var json = VocabularyExtractionService.BuildExtractionContext(
            NewProfile(), activity: null, module: null,
            submittedContent: "answer",
            feedbackJson: feedbackJson,
            improvedVersion: null,
            knownTerms: []);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("feedbackChanges").GetArrayLength().Should().Be(0);
    }
}
