using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Activity.Evaluators;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Activity;

public sealed class KeyedSelectionEvaluatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KeyedSelectionEvaluator _sut = new();

    private static PatternEvaluationRequest MakeRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "phrase_match",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.MatchingPairs,
            ActivityType: ActivityType.WritingScenario,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string PhraseContent(int pairCount)
    {
        var pairs = Enumerable.Range(0, pairCount)
            .Select(i => new PhraseMatchPairDto { Phrase = $"Phrase{i}", Meaning = $"Meaning{i}" })
            .ToList();
        return JsonSerializer.Serialize(new PhraseMatchContent { Pairs = pairs }, JsonOptions);
    }

    /// <summary>Builds a submitted pair map: phrase_N -> meaning_M per entry.</summary>
    private static string Submitted(params (int phraseIdx, int meaningIdx)[] selections)
    {
        var d = selections.ToDictionary(
            s => $"phrase_{s.phraseIdx}",
            s => (string?)$"meaning_{s.meaningIdx}");
        return JsonSerializer.Serialize(new PhraseMatchSubmittedAnswer { Pairs = d }, JsonOptions);
    }

    // ── MarkingMode ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkingMode_IsKeyedSelection()
    {
        _sut.MarkingMode.Should().Be(MarkingMode.KeyedSelection);
    }

    // ── all correct ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllCorrect_ReturnsFullScore()
    {
        var content = PhraseContent(3);
        var submitted = Submitted((0, 0), (1, 1), (2, 2));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(3);
        result.MaxScore.Should().Be(3);
        result.Percentage.Should().Be(100);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Where(i => i.MaxScore > 0).Should().AllSatisfy(i => i.IsCorrect.Should().BeTrue());
    }

    // ── partial credit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PartialCredit_SomeCorrect()
    {
        var content = PhraseContent(3);
        var submitted = Submitted((0, 0), (1, 2), (2, 1)); // 1 correct, 2 wrong

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(3);
    }

    // ── missing pairs ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MissingPairs_ScoredAsIncorrect_WithFeedback()
    {
        var content = PhraseContent(2);
        var submitted = Submitted((0, 0)); // phrase_1 missing

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        var missing = result.ItemResults.Single(i => i.ItemKey == "phrase_1");
        missing.IsCorrect.Should().BeFalse();
        missing.StudentAnswer.Should().BeNull();
        missing.Feedback.Should().Contain("Missing");
    }

    // ── unknown keys ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownSubmittedKey_AppearsAsZeroMaxScore_InResults()
    {
        var content = PhraseContent(1);
        var submitted = JsonSerializer.Serialize(new PhraseMatchSubmittedAnswer
        {
            Pairs = new Dictionary<string, string?>
            {
                ["phrase_0"] = "meaning_0",
                ["phrase_99"] = "meaning_0" // unknown
            }
        }, JsonOptions);

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        var unknown = result.ItemResults.SingleOrDefault(i => i.ItemKey == "phrase_99");
        unknown.Should().NotBeNull();
        unknown!.MaxScore.Should().Be(0);
        unknown.IsCorrect.Should().BeFalse();
    }

    // ── duplicate / wrong meaning key ─────────────────────────────────────────

    [Fact]
    public async Task WrongMeaningKey_ScoredAsIncorrect()
    {
        var content = PhraseContent(2);
        var submitted = Submitted((0, 1), (1, 0)); // swapped

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.ItemResults.Where(i => i.MaxScore > 0).Should().AllSatisfy(i => i.IsCorrect.Should().BeFalse());
    }

    // ── completed always true ──────────────────────────────────────────────────

    [Fact]
    public async Task AllWrong_IsStillCompleted()
    {
        var content = PhraseContent(2);
        var submitted = Submitted((0, 1), (1, 0));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Completed.Should().BeTrue();
    }

    // ── no AI dependency ───────────────────────────────────────────────────────

    [Fact]
    public async Task DoesNotRequireAiDependency()
    {
        var evaluator = new KeyedSelectionEvaluator();
        var content = PhraseContent(1);
        var submitted = Submitted((0, 0));

        var result = await evaluator.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Should().NotBeNull();
    }

    // ── staged module_stage_v1 unwrapping ─────────────────────────────────────

    private static string StagedPhraseContent(int pairCount)
    {
        var pairs = Enumerable.Range(0, pairCount)
            .Select(i => new { phrase = $"Phrase{i}", meaning = $"Meaning{i}", context = $"Context{i}" })
            .ToArray();
        var staged = new
        {
            schemaVersion = "module_stage_v1",
            title = "Test",
            learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
            practiceContent = new
            {
                instructions = "Match each phrase.",
                scenario = (string?)null,
                task = "Match.",
                exerciseData = new { pairs }
            },
            feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
        };
        return JsonSerializer.Serialize(staged, JsonOptions);
    }

    [Fact]
    public async Task StagedContent_AllCorrect_ReturnsFullScore()
    {
        var content = StagedPhraseContent(3);
        var submitted = Submitted((0, 0), (1, 1), (2, 2));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(3);
        result.Passed.Should().BeTrue();
        result.ItemResults.Should().AllSatisfy(r => r.IsCorrect.Should().BeTrue());
    }

    [Fact]
    public async Task StagedContent_AllWrong_ReturnsZeroScore()
    {
        var content = StagedPhraseContent(2);
        var submitted = Submitted((0, 1), (1, 0));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task LegacyFlatContent_StillEvaluatesCorrectly()
    {
        // Ensure old flat-format activities continue to work after staged unwrapping was added.
        var content = PhraseContent(2);
        var submitted = Submitted((0, 0), (1, 1));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(2);
        result.Passed.Should().BeTrue();
    }
}
