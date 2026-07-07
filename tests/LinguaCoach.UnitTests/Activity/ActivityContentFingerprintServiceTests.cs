using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Infrastructure.Activity;

namespace LinguaCoach.UnitTests.Activity;

public sealed class ActivityContentFingerprintServiceTests
{
    private readonly ActivityContentFingerprintService _sut = new();

    private const string ModuleStageJson = """
    {
      "schemaVersion": "module_stage_v1",
      "primarySkill": "writing",
      "learnContent": {
        "teachingTitle": "Following up on a delay",
        "explanation": "Use polite language to ask for an update.",
        "keyPoints": ["Be polite", "Be specific"],
        "examples": [{ "phrase": "Could you please provide an update?", "meaning": "polite request", "note": null }]
      },
      "practiceContent": {
        "instructions": "Write a follow-up email.",
        "scenario": "Your vendor is late on a shipment.",
        "task": "Write a polite follow-up email.",
        "exerciseData": {}
      },
      "feedbackPlan": {
        "evaluationCriteria": ["politeness", "clarity"],
        "rubric": [],
        "feedbackFocus": null,
        "successCriteria": ["Clear ask", "Polite tone"]
      }
    }
    """;

    private const string FormIoJson = """
    {
      "display": "form",
      "components": [
        { "type": "radio", "key": "answer", "label": "Which is correct?", "values": [{ "label": "am", "value": "A" }, { "label": "is", "value": "B" }] }
      ]
    }
    """;

    [Fact]
    public void ModuleStageSchema_SameContent_ProducesSameFingerprint()
    {
        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema, PatternKey: "email_reply"));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema, PatternKey: "email_reply"));

        first.Should().Be(second);
    }

    [Fact]
    public void FormIoSchema_SameContent_ProducesSameFingerprint()
    {
        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            FormIoJson, ActivityContentShape.FormIoSchema, PatternKey: "grammar_quiz"));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            FormIoJson, ActivityContentShape.FormIoSchema, PatternKey: "grammar_quiz"));

        first.Should().Be(second);
    }

    [Fact]
    public void ChangedScenario_ProducesDifferentFingerprint()
    {
        var changed = ModuleStageJson.Replace(
            "Your vendor is late on a shipment.", "Your colleague missed a deadline.");

        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            changed, ActivityContentShape.ModuleStageSchema));

        first.Should().NotBe(second);
    }

    [Fact]
    public void ChangedTopicKeyMetadata_ProducesDifferentFingerprint()
    {
        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema, TopicKey: "vendor-delay"));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema, TopicKey: "deadline-miss"));

        first.Should().NotBe(second);
    }

    [Fact]
    public void ChangedPromptKey_ProducesDifferentFingerprint()
    {
        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema, PromptKey: "prompt_a"));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema, PromptKey: "prompt_b"));

        first.Should().NotBe(second);
    }

    [Fact]
    public void WhitespaceAndCasingDifferences_DoNotAffectFingerprint()
    {
        var reformatted = """
        {
          "SCHEMAVERSION":   "module_stage_v1"   ,
          "primarySkill": "WRITING",
          "learnContent": {
            "teachingTitle":    "following up on a delay",
            "explanation": "Use   polite   language to ask for an update.",
            "keyPoints": ["be polite", "Be specific"],
            "examples": [{ "phrase": "Could you please provide an update?", "meaning": "polite request", "note": null }]
          },
          "practiceContent": {
            "instructions": "Write a follow-up email.",
            "scenario": "Your vendor is late on a shipment.",
            "task": "Write a polite follow-up email.",
            "exerciseData": {}
          },
          "feedbackPlan": {
            "evaluationCriteria": ["politeness", "clarity"],
            "rubric": [],
            "feedbackFocus": null,
            "successCriteria": ["Clear ask", "Polite tone"]
          }
        }
        """;

        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            reformatted, ActivityContentShape.ModuleStageSchema));

        first.Should().Be(second);
    }

    [Fact]
    public void VolatileIdAndTimestampFields_DoNotAffectFingerprint()
    {
        var withVolatileFields = """
        {
          "id": "11111111-1111-1111-1111-111111111111",
          "createdAtUtc": "2026-07-08T00:00:00Z",
          "schemaVersion": "module_stage_v1",
          "primarySkill": "writing",
          "learnContent": {
            "teachingTitle": "Following up on a delay",
            "explanation": "Use polite language to ask for an update.",
            "keyPoints": ["Be polite", "Be specific"],
            "examples": [{ "phrase": "Could you please provide an update?", "meaning": "polite request", "note": null }]
          },
          "practiceContent": {
            "instructions": "Write a follow-up email.",
            "scenario": "Your vendor is late on a shipment.",
            "task": "Write a polite follow-up email.",
            "exerciseData": {}
          },
          "feedbackPlan": {
            "evaluationCriteria": ["politeness", "clarity"],
            "rubric": [],
            "feedbackFocus": null,
            "successCriteria": ["Clear ask", "Polite tone"]
          }
        }
        """;

        var first = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ModuleStageJson, ActivityContentShape.ModuleStageSchema));
        var second = _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            withVolatileFields, ActivityContentShape.ModuleStageSchema));

        first.Should().Be(second);
    }

    [Fact]
    public void NullContentJson_FallsBackToMetadataOnlyFingerprint_WithoutThrowing()
    {
        var act = () => _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ContentJson: null, ActivityContentShape.Unknown, PatternKey: "x"));

        act.Should().NotThrow();
    }

    [Fact]
    public void InvalidJson_FallsBackToTextHash_WithoutThrowing()
    {
        var act = () => _sut.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ContentJson: "not valid json {{{", ActivityContentShape.Unknown));

        act.Should().NotThrow();
    }
}
