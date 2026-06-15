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

    // ── select_missing_word ────────────────────────────────────────────────────

    private static PatternEvaluationRequest MakeSelectMissingWordRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "select_missing_word",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.MultipleChoice,
            ActivityType: ActivityType.ListeningComprehension,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string StagedSelectMissingWordContent(string correctOptionId = "A") => JsonSerializer.Serialize(new
    {
        schemaVersion = "module_stage_v1",
        title = "Test select missing word",
        learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
        practiceContent = new
        {
            instructions = "Listen and choose the missing word.",
            scenario = "Shift handover",
            task = "Choose the missing word or phrase.",
            exerciseData = new
            {
                audioScript = "Before you leave, please make sure the report is submitted by six o'clock.",
                audioUrl = (string?)null,
                incompleteText = "Before you leave, please make sure the report is {{missing}} by six o'clock.",
                question = "Choose the missing word or phrase.",
                options = new[]
                {
                    new { id = "A", text = "submitted" },
                    new { id = "B", text = "ignored" },
                    new { id = "C", text = "cancelled" },
                    new { id = "D", text = "forgotten" },
                },
                correctOptionId,
                explanation = "The audio says the report is 'submitted by six o'clock'.",
                distractorExplanations = new Dictionary<string, string>
                {
                    ["B"] = "Ignoring the report does not match the instruction.",
                    ["C"] = "The report is not cancelled in the audio.",
                    ["D"] = "The supervisor reminds the team to submit, not forget.",
                },
                successChecklist = new[] { "Selected the supported option" },
            },
        },
        feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
    }, JsonOptions);

    [Fact]
    public async Task SelectMissingWord_CorrectSelection_ReturnsFullScore()
    {
        var content = StagedSelectMissingWordContent(correctOptionId: "A");
        var submitted = SelectedOption("A");

        var result = await _sut.EvaluateAsync(MakeSelectMissingWordRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
        result.ItemResults.Single().Feedback.Should().Contain("submitted");
    }

    [Fact]
    public async Task SelectMissingWord_IncorrectSelection_ReturnsZeroScore_WithDistractorExplanation()
    {
        var content = StagedSelectMissingWordContent(correctOptionId: "A");
        var submitted = SelectedOption("B");

        var result = await _sut.EvaluateAsync(MakeSelectMissingWordRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
        var item = result.ItemResults.Single();
        item.IsCorrect.Should().BeFalse();
        item.CorrectAnswer.Should().Be("A");
        item.Feedback.Should().Contain("Ignoring the report does not match the instruction");
    }

    [Fact]
    public async Task SelectMissingWord_NoSelection_ReturnsZeroScore_AndIsStillCompleted()
    {
        var content = StagedSelectMissingWordContent(correctOptionId: "A");

        var result = await _sut.EvaluateAsync(MakeSelectMissingWordRequest(content, submittedJson: ""), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().StudentAnswer.Should().BeNull();
    }

    // ── highlight_correct_summary ───────────────────────────────────────────────

    private static PatternEvaluationRequest MakeHighlightCorrectSummaryRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "highlight_correct_summary",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.HighlightCorrectSummary,
            ActivityType: ActivityType.ListeningComprehension,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string StagedHighlightCorrectSummaryContent(string correctOptionId = "A") => JsonSerializer.Serialize(new
    {
        schemaVersion = "module_stage_v1",
        title = "Test highlight correct summary",
        learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
        practiceContent = new
        {
            instructions = "Listen and choose the best summary.",
            scenario = "Project status update",
            task = "Listen and choose the best summary.",
            exerciseData = new
            {
                audioScript = "The redesign is on track and we'll ship next Friday. The budget is unchanged.",
                audioUrl = (string?)null,
                question = "Which summary best matches the audio?",
                options = new[]
                {
                    new { id = "A", text = "The redesign is on track to ship next Friday with no budget change." },
                    new { id = "B", text = "The redesign is delayed and the budget increased." },
                    new { id = "C", text = "The redesign shipped last Friday." },
                    new { id = "D", text = "The redesign was cancelled." },
                },
                correctOptionId,
                explanation = "The speaker says the redesign is on track to ship next Friday with the budget unchanged.",
                distractorExplanations = new Dictionary<string, string>
                {
                    ["B"] = "The audio says the work is on track and the budget is unchanged.",
                    ["C"] = "The release is next Friday, not last Friday.",
                    ["D"] = "Nothing was cancelled in the audio.",
                },
                successChecklist = new[] { "Selected the supported summary" },
            },
        },
        feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
    }, JsonOptions);

    [Fact]
    public async Task HighlightCorrectSummary_CorrectSelection_ReturnsFullScore()
    {
        var content = StagedHighlightCorrectSummaryContent(correctOptionId: "A");
        var submitted = SelectedOption("A");

        var result = await _sut.EvaluateAsync(MakeHighlightCorrectSummaryRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
        result.ItemResults.Single().Feedback.Should().Contain("next Friday");
    }

    [Fact]
    public async Task HighlightCorrectSummary_IncorrectSelection_ReturnsZeroScore_WithDistractorExplanation()
    {
        var content = StagedHighlightCorrectSummaryContent(correctOptionId: "A");
        var submitted = SelectedOption("B");

        var result = await _sut.EvaluateAsync(MakeHighlightCorrectSummaryRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
        var item = result.ItemResults.Single();
        item.IsCorrect.Should().BeFalse();
        item.CorrectAnswer.Should().Be("A");
        item.Feedback.Should().Contain("the budget is unchanged");
    }

    [Fact]
    public async Task HighlightCorrectSummary_NoSelection_ReturnsZeroScore_AndIsStillCompleted()
    {
        var content = StagedHighlightCorrectSummaryContent(correctOptionId: "A");

        var result = await _sut.EvaluateAsync(MakeHighlightCorrectSummaryRequest(content, submittedJson: ""), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().StudentAnswer.Should().BeNull();
    }

    [Fact]
    public async Task HighlightCorrectSummary_InvalidJson_HandledSafely()
    {
        var content = StagedHighlightCorrectSummaryContent(correctOptionId: "A");

        var result = await _sut.EvaluateAsync(MakeHighlightCorrectSummaryRequest(content, "not-json"), default);

        result.Completed.Should().BeTrue();
        result.Score.Should().Be(0);
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

    // ── listening_multiple_choice_multi ───────────────────────────────────────

    private static PatternEvaluationRequest MakeListeningMultiRequest(string contentJson, string submittedJson) =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "listening_multiple_choice_multi",
            MarkingMode: MarkingMode.KeyedSelection,
            InteractionMode: InteractionMode.MultipleChoiceMulti,
            ActivityType: ActivityType.ListeningComprehension,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string StagedListeningMultiContent(string[] correctOptionIds) => JsonSerializer.Serialize(new
    {
        schemaVersion = "module_stage_v1",
        title = "Test listening multi",
        learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
        practiceContent = new
        {
            instructions = "Listen and choose all correct answers.",
            scenario = "Project update",
            task = "Select all supported answers.",
            exerciseData = new
            {
                audioScript = "The deadline moved to next Friday, and we're adding two more developers. The budget remains unchanged.",
                audioUrl = (string?)null,
                question = "Which changes were announced?",
                options = new[]
                {
                    new { id = "A", text = "The deadline moved to next Friday" },
                    new { id = "B", text = "Two more developers were added" },
                    new { id = "C", text = "The budget was increased" },
                    new { id = "D", text = "The project was cancelled" },
                },
                correctOptionIds,
                explanation = "The deadline moved and developers were added, while the budget stayed the same.",
                optionExplanations = new Dictionary<string, string>
                {
                    ["A"] = "Correct — the deadline moved to next Friday.",
                    ["B"] = "Correct — two more developers were added.",
                    ["C"] = "Incorrect — the budget remains unchanged.",
                    ["D"] = "Incorrect — the project was not cancelled.",
                },
            },
        },
        feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
    }, JsonOptions);

    [Fact]
    public async Task ListeningMultiChoiceMulti_ExactCorrectSet_ReturnsFullScore()
    {
        var content = StagedListeningMultiContent(["A", "B"]);
        var submitted = SelectedOptions("A", "B");

        var result = await _sut.EvaluateAsync(MakeListeningMultiRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ListeningMultiChoiceMulti_MissingOneCorrect_ReturnsIncorrect()
    {
        var content = StagedListeningMultiContent(["A", "B"]);
        var submitted = SelectedOptions("A");  // missed B

        var result = await _sut.EvaluateAsync(MakeListeningMultiRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().Feedback.Should().Contain("missed");
    }

    [Fact]
    public async Task ListeningMultiChoiceMulti_FalsePositive_ReturnsIncorrect()
    {
        var content = StagedListeningMultiContent(["A", "B"]);
        var submitted = SelectedOptions("A", "B", "C");  // C is wrong

        var result = await _sut.EvaluateAsync(MakeListeningMultiRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.Passed.Should().BeFalse();
        result.ItemResults.Single().Feedback.Should().Contain("C");
    }

    [Fact]
    public async Task ListeningMultiChoiceMulti_NoSelection_ReturnsIncorrect_StillCompleted()
    {
        var content = StagedListeningMultiContent(["A", "B"]);

        var result = await _sut.EvaluateAsync(MakeListeningMultiRequest(content, ""), default);

        result.Score.Should().Be(0);
        result.Completed.Should().BeTrue();
        result.ItemResults.Single().StudentAnswer.Should().BeNull();
    }

    [Fact]
    public async Task ListeningMultiChoiceMulti_InvalidJson_HandledSafely()
    {
        var content = StagedListeningMultiContent(["A", "B"]);

        var result = await _sut.EvaluateAsync(MakeListeningMultiRequest(content, "not-json"), default);

        result.Completed.Should().BeTrue();
        result.Score.Should().Be(0);
    }
}
