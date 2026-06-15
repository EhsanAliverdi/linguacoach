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

    // ── reading_multiple_choice_single ────────────────────────────────────────

    private static PatternEvaluationRequest MakeReadingRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "reading_multiple_choice_single",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.MultipleChoice,
            ActivityType: ActivityType.ReadingTask,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string StagedReadingContent(string correctOptionId = "B") => JsonSerializer.Serialize(new
    {
        schemaVersion = "module_stage_v1",
        title = "Test reading",
        learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
        practiceContent = new
        {
            instructions = "Read and choose.",
            scenario = "Workplace email",
            task = "Choose the best answer.",
            exerciseData = new
            {
                passage = "The launch was delayed because of testing.",
                question = "Why was the launch delayed?",
                options = new[]
                {
                    new { id = "A", text = "Budget issues" },
                    new { id = "B", text = "Additional testing needed" },
                    new { id = "C", text = "Staff holiday" },
                    new { id = "D", text = "Office relocation" },
                },
                correctOptionId,
                explanation = "The passage says testing caused the delay.",
                distractorExplanations = new Dictionary<string, string>
                {
                    ["A"] = "Budget is not mentioned.",
                    ["C"] = "Holidays are not mentioned.",
                    ["D"] = "Relocation is not mentioned.",
                },
                successChecklist = new[] { "Selected the supported option" },
            },
        },
        feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
    }, JsonOptions);

    private static string SelectedOption(string optionId) =>
        JsonSerializer.Serialize(new ReadingMultipleChoiceSubmittedAnswer { SelectedOptionId = optionId }, JsonOptions);

    [Fact]
    public async Task ReadingMultipleChoice_CorrectSelection_ReturnsFullScore()
    {
        var content = StagedReadingContent(correctOptionId: "B");
        var submitted = SelectedOption("B");

        var result = await _sut.EvaluateAsync(MakeReadingRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
        result.ItemResults.Single().Feedback.Should().Contain("testing");
    }

    [Fact]
    public async Task ReadingMultipleChoice_IncorrectSelection_ReturnsZeroScore_WithDistractorExplanation()
    {
        var content = StagedReadingContent(correctOptionId: "B");
        var submitted = SelectedOption("A");

        var result = await _sut.EvaluateAsync(MakeReadingRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
        var item = result.ItemResults.Single();
        item.IsCorrect.Should().BeFalse();
        item.CorrectAnswer.Should().Be("B");
        item.Feedback.Should().Contain("Budget is not mentioned");
    }

    [Fact]
    public async Task ReadingMultipleChoice_NoSelection_ReturnsZeroScore_AndIsStillCompleted()
    {
        var content = StagedReadingContent(correctOptionId: "B");

        var result = await _sut.EvaluateAsync(MakeReadingRequest(content, submittedJson: ""), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().StudentAnswer.Should().BeNull();
    }

    // ── listening_multiple_choice_single ──────────────────────────────────────

    private static PatternEvaluationRequest MakeListeningRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "listening_multiple_choice_single",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.MultipleChoice,
            ActivityType: ActivityType.ListeningComprehension,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string StagedListeningContent(string correctOptionId = "B") => JsonSerializer.Serialize(new
    {
        schemaVersion = "module_stage_v1",
        title = "Test listening",
        learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
        practiceContent = new
        {
            instructions = "Listen and choose.",
            scenario = "Team update",
            task = "Choose the best answer.",
            exerciseData = new
            {
                audioScript = "We were planning to release on Friday, but instead we'll release next Monday.",
                audioUrl = (string?)null,
                question = "When will the release happen?",
                options = new[]
                {
                    new { id = "A", text = "Friday" },
                    new { id = "B", text = "Next Monday" },
                    new { id = "C", text = "Cancelled" },
                    new { id = "D", text = "Immediately" },
                },
                correctOptionId,
                explanation = "The speaker says the release moved to next Monday.",
                distractorExplanations = new Dictionary<string, string>
                {
                    ["A"] = "Friday was the original plan, which changed.",
                    ["C"] = "The release was not cancelled.",
                    ["D"] = "The release is not immediate.",
                },
                successChecklist = new[] { "Selected the supported option" },
            },
        },
        feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
    }, JsonOptions);

    [Fact]
    public async Task ListeningMultipleChoice_CorrectSelection_ReturnsFullScore()
    {
        var content = StagedListeningContent(correctOptionId: "B");
        var submitted = SelectedOption("B");

        var result = await _sut.EvaluateAsync(MakeListeningRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
        result.ItemResults.Single().Feedback.Should().Contain("Monday");
    }

    [Fact]
    public async Task ListeningMultipleChoice_IncorrectSelection_ReturnsZeroScore_WithDistractorExplanation()
    {
        var content = StagedListeningContent(correctOptionId: "B");
        var submitted = SelectedOption("A");

        var result = await _sut.EvaluateAsync(MakeListeningRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
        var item = result.ItemResults.Single();
        item.IsCorrect.Should().BeFalse();
        item.CorrectAnswer.Should().Be("B");
        item.Feedback.Should().Contain("Friday was the original plan");
    }

    [Fact]
    public async Task ListeningMultipleChoice_NoSelection_ReturnsZeroScore_AndIsStillCompleted()
    {
        var content = StagedListeningContent(correctOptionId: "B");

        var result = await _sut.EvaluateAsync(MakeListeningRequest(content, submittedJson: ""), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().StudentAnswer.Should().BeNull();
    }

    // ── reading_multiple_choice_multi ─────────────────────────────────────────

    private static PatternEvaluationRequest MakeReadingMultiRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "reading_multiple_choice_multi",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.MultipleChoiceMulti,
            ActivityType: ActivityType.ReadingTask,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string StagedReadingMultiContent(string[] correctOptionIds) => JsonSerializer.Serialize(new
    {
        schemaVersion = "module_stage_v1",
        title = "Test reading multi",
        learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
        practiceContent = new
        {
            instructions = "Read and choose all correct answers.",
            scenario = "Workplace email",
            task = "Select all supported answers.",
            exerciseData = new
            {
                passage = "The project was delayed due to testing and budget approval.",
                question = "Why was the project delayed?",
                options = new[]
                {
                    new { id = "A", text = "Testing issues" },
                    new { id = "B", text = "Staff holiday" },
                    new { id = "C", text = "Budget approval" },
                    new { id = "D", text = "Office relocation" },
                },
                correctOptionIds,
                explanation = "Testing and budget approval are both mentioned in the passage.",
                optionExplanations = new Dictionary<string, string>
                {
                    ["A"] = "Correct — testing is mentioned.",
                    ["B"] = "Incorrect — holidays are not mentioned.",
                    ["C"] = "Correct — budget approval is mentioned.",
                    ["D"] = "Incorrect — relocation is not mentioned.",
                },
            },
        },
        feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
    }, JsonOptions);

    private static string SelectedOptions(params string[] ids) =>
        JsonSerializer.Serialize(new ReadingMultipleChoiceMultiSubmittedAnswer { SelectedOptionIds = [.. ids] }, JsonOptions);

    [Fact]
    public async Task ReadingMultiChoiceMulti_ExactCorrectSet_ReturnsFullScore()
    {
        var content = StagedReadingMultiContent(["A", "C"]);
        var submitted = SelectedOptions("A", "C");

        var result = await _sut.EvaluateAsync(MakeReadingMultiRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ReadingMultiChoiceMulti_MissingOneCorrect_ReturnsIncorrect()
    {
        var content = StagedReadingMultiContent(["A", "C"]);
        var submitted = SelectedOptions("A");  // missed C

        var result = await _sut.EvaluateAsync(MakeReadingMultiRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().Feedback.Should().Contain("missed");
    }

    [Fact]
    public async Task ReadingMultiChoiceMulti_FalsePositive_ReturnsIncorrect()
    {
        var content = StagedReadingMultiContent(["A", "C"]);
        var submitted = SelectedOptions("A", "B", "C");  // B is wrong

        var result = await _sut.EvaluateAsync(MakeReadingMultiRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.Passed.Should().BeFalse();
        result.ItemResults.Single().Feedback.Should().Contain("B");
    }

    [Fact]
    public async Task ReadingMultiChoiceMulti_NoSelection_ReturnsIncorrect_StillCompleted()
    {
        var content = StagedReadingMultiContent(["A", "C"]);

        var result = await _sut.EvaluateAsync(MakeReadingMultiRequest(content, ""), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().StudentAnswer.Should().BeNull();
    }

    [Fact]
    public async Task ReadingMultiChoiceMulti_InvalidJson_HandledSafely()
    {
        var content = StagedReadingMultiContent(["A", "C"]);

        var result = await _sut.EvaluateAsync(MakeReadingMultiRequest(content, "not-json"), default);

        result.Completed.Should().BeTrue();
        result.Score.Should().Be(0);
    }
}
