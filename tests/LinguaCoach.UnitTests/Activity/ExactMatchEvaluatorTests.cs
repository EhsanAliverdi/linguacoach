using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Activity.Evaluators;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Activity;

public sealed class ExactMatchEvaluatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ExactMatchEvaluator _sut = new();

    private static PatternEvaluationRequest MakeRequest(
        string contentJson,
        string submittedJson,
        string patternKey = "gap_fill_workplace_phrase") =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: patternKey,
            MarkingMode: MarkingMode.ExactMatch,
            InteractionMode: InteractionMode.GapFill,
            ActivityType: ActivityType.WritingScenario,
            ContentJson: contentJson,
            SubmittedAnswerJson: submittedJson);

    private static string GapFillContent(params string[] answers)
    {
        var items = answers.Select((a, i) => new GapFillItemDto
        {
            Sentence = $"Sentence {i + 1}",
            Answer = a
        }).ToList();
        return JsonSerializer.Serialize(new GapFillWorkplacePhraseContent { Items = items }, JsonOptions);
    }

    private static string Submitted(params (string key, string? val)[] pairs)
    {
        var d = pairs.ToDictionary(p => p.key, p => p.val);
        return JsonSerializer.Serialize(new GapFillSubmittedAnswer { Answers = d! }, JsonOptions);
    }

    // ── MarkingMode ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkingMode_IsExactMatch()
    {
        _sut.MarkingMode.Should().Be(MarkingMode.ExactMatch);
    }

    // ── correct answers ────────────────────────────────────────────────────────

    [Fact]
    public async Task AllCorrect_ReturnsFullScore()
    {
        var content = GapFillContent("confirm", "check");
        var submitted = Submitted(("gap_1", "confirm"), ("gap_2", "check"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(2);
        result.MaxScore.Should().Be(2);
        result.Percentage.Should().Be(100);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Should().AllSatisfy(i => i.IsCorrect.Should().BeTrue());
    }

    // ── case / whitespace / punctuation normalization ──────────────────────────

    [Fact]
    public async Task CaseInsensitive_MatchesCorrectly()
    {
        var content = GapFillContent("confirm");
        var submitted = Submitted(("gap_1", "CONFIRM"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.ItemResults[0].IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ExtraWhitespace_NormalizesAndMatches()
    {
        var content = GapFillContent("send it");
        var submitted = Submitted(("gap_1", "  send   it  "));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task TrailingPunctuation_NormalizesAndMatches()
    {
        var content = GapFillContent("confirm");
        var submitted = Submitted(("gap_1", "confirm."));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
    }

    // ── accepted alternatives ──────────────────────────────────────────────────

    [Fact]
    public async Task AlternativeAnswer_ViaSeparator_IsAccepted()
    {
        // Answer field encodes "confirm / check" — both should be accepted
        var content = GapFillContent("confirm / check");
        var submitted = Submitted(("gap_1", "check"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
        result.ItemResults[0].AcceptedAnswers.Should().Contain("check");
    }

    [Fact]
    public async Task AlternativeAnswer_ViaPipe_IsAccepted()
    {
        var content = GapFillContent("confirm|verify");
        var submitted = Submitted(("gap_1", "verify"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
    }

    // ── partial credit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PartialCredit_SomeCorrect()
    {
        var content = GapFillContent("confirm", "send", "check");
        var submitted = Submitted(("gap_1", "confirm"), ("gap_2", "wrong"), ("gap_3", "check"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(2);
        result.MaxScore.Should().Be(3);
        result.Percentage.Should().BeApproximately(66.67, 0.01);
        result.Passed.Should().BeTrue();
    }

    // ── missing / extra answers ────────────────────────────────────────────────

    [Fact]
    public async Task MissingAnswer_ScoredAsIncorrect()
    {
        var content = GapFillContent("confirm", "check");
        var submitted = Submitted(("gap_1", "confirm")); // gap_2 missing

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.ItemResults[1].IsCorrect.Should().BeFalse();
        result.ItemResults[1].StudentAnswer.Should().BeNull();
    }

    [Fact]
    public async Task ExtraSubmittedAnswers_DoNotAffectScore()
    {
        var content = GapFillContent("confirm");
        var submitted = Submitted(("gap_1", "confirm"), ("gap_99", "extra"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
    }

    // ── listen_and_gap_fill path ───────────────────────────────────────────────

    [Fact]
    public async Task ListenAndGapFill_UsesGapIdAsKey()
    {
        var gaps = new List<ListenAndGapFillItemDto>
        {
            new() { Id = "g1", SentenceWithBlank = "__ said yes", Answer = "She" },
            new() { Id = "g2", SentenceWithBlank = "Go __ now", Answer = "home" }
        };
        var contentJson = JsonSerializer.Serialize(
            new ListenAndGapFillContent { Gaps = gaps }, JsonOptions);
        var submitted = Submitted(("g1", "She"), ("g2", "home"));

        var result = await _sut.EvaluateAsync(
            MakeRequest(contentJson, submitted, "listen_and_gap_fill"), default);

        result.Score.Should().Be(2);
        result.Completed.Should().BeTrue();
    }

    // ── completed flag ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AllWrong_IsStillCompleted()
    {
        var content = GapFillContent("confirm");
        var submitted = Submitted(("gap_1", "wrong"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Completed.Should().BeTrue();
        result.Score.Should().Be(0);
    }

    // ── no AI dependency ───────────────────────────────────────────────────────

    [Fact]
    public async Task DoesNotRequireAiDependency_CompletesWithoutInjection()
    {
        // Evaluator is instantiated with no dependencies — confirms no AI call path
        var evaluator = new ExactMatchEvaluator();
        var content = GapFillContent("confirm");
        var submitted = Submitted(("gap_1", "confirm"));

        var result = await evaluator.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Should().NotBeNull();
    }

    // ── staged module_stage_v1 unwrapping ─────────────────────────────────────

    private static string StagedGapFillContent(params string[] answers)
    {
        var items = answers.Select((a, i) => new { sentence = $"Sentence {i + 1} ___", answer = a, distractors = Array.Empty<string>(), hint = (string?)null }).ToArray();
        var staged = new
        {
            schemaVersion = "module_stage_v1",
            title = "Test",
            learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
            practiceContent = new
            {
                instructions = "Fill in the blanks.",
                scenario = (string?)null,
                task = "Complete.",
                exerciseData = new { items }
            },
            feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
        };
        return JsonSerializer.Serialize(staged, JsonOptions);
    }

    [Fact]
    public async Task StagedContent_AllCorrect_ReturnsFullScore()
    {
        var content = StagedGapFillContent("confirm", "schedule");
        var submitted = Submitted(("gap_1", "confirm"), ("gap_2", "schedule"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(2);
        result.Passed.Should().BeTrue();
        result.ItemResults.Should().AllSatisfy(r => r.IsCorrect.Should().BeTrue());
    }

    [Fact]
    public async Task StagedContent_OneWrong_ReturnsPartialScore()
    {
        var content = StagedGapFillContent("confirm", "schedule");
        var submitted = Submitted(("gap_1", "confirm"), ("gap_2", "postpone"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task LegacyFlatContent_StillEvaluatesCorrectly()
    {
        var content = GapFillContent("confirm");
        var submitted = Submitted(("gap_1", "confirm"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.Passed.Should().BeTrue();
    }

    // ── normalization unit tests ───────────────────────────────────────────────

    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("  hello  ", "hello")]
    [InlineData("hello.", "hello")]
    [InlineData("hello,", "hello")]
    [InlineData("hello!?", "hello")]
    [InlineData("hello world", "hello world")]
    [InlineData("hello  world", "hello world")]
    public void Normalize_AppliesCorrectTransformations(string input, string expected)
    {
        ExactMatchEvaluator.Normalize(input).Should().Be(expected);
    }

    // ── reading_fill_in_blanks ────────────────────────────────────────────────

    private static string ReadingFillInBlanksContent(params (string id, string answer, string[] options)[] gaps)
    {
        var gapDtos = gaps.Select(g => new ReadingFillInBlanksGapDto
        {
            Id = g.id,
            Answer = g.answer,
            Options = g.options.ToList(),
        }).ToList();
        var content = new LinguaCoach.Application.Activity.ReadingFillInBlanksContent
        {
            PassageWithBlanks = "The {{gap1}} ran quickly.",
            Gaps = gapDtos,
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    private static string FillBlanksSubmitted(params (string key, string? val)[] pairs)
    {
        var d = pairs.ToDictionary(p => p.key, p => p.val);
        return JsonSerializer.Serialize(new GapFillSubmittedAnswer { Answers = d! }, JsonOptions);
    }

    [Fact]
    public async Task ReadingFillInBlanks_AllCorrect_ReturnsFullScore()
    {
        var content = ReadingFillInBlanksContent(
            ("gap1", "dog", ["dog", "cat", "bird", "fish"]),
            ("gap2", "quickly", ["slowly", "quickly", "quietly", "loudly"]));
        var submitted = FillBlanksSubmitted(("gap1", "dog"), ("gap2", "quickly"));

        var request = MakeRequest(content, submitted, "reading_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.Score.Should().Be(2);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeTrue();
        result.ItemResults.Should().AllSatisfy(i => i.IsCorrect.Should().BeTrue());
    }

    [Fact]
    public async Task ReadingFillInBlanks_OneWrong_ReturnsPartialScore()
    {
        var content = ReadingFillInBlanksContent(
            ("gap1", "dog", ["dog", "cat", "bird", "fish"]),
            ("gap2", "quickly", ["slowly", "quickly", "quietly", "loudly"]));
        var submitted = FillBlanksSubmitted(("gap1", "cat"), ("gap2", "quickly"));

        var request = MakeRequest(content, submitted, "reading_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeFalse();
        result.ItemResults.Should().ContainSingle(i => i.IsCorrect);
    }

    [Fact]
    public async Task ReadingFillInBlanks_NormalizesCase()
    {
        var content = ReadingFillInBlanksContent(("gap1", "Dog", ["Dog", "Cat"]));
        var submitted = FillBlanksSubmitted(("gap1", "dog"));

        var request = MakeRequest(content, submitted, "reading_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    // ── listening_fill_in_blanks ──────────────────────────────────────────────

    private static string ListeningFillInBlanksContent(params (string id, string answer, string[] accepted, string[] options)[] gaps)
    {
        var gapDtos = gaps.Select(g => new ListeningFillInBlanksGapDto
        {
            Id = g.id,
            Answer = g.answer,
            AcceptedAnswers = g.accepted.ToList(),
            Options = g.options.ToList(),
        }).ToList();
        var content = new LinguaCoach.Application.Activity.ListeningFillInBlanksContent
        {
            AudioScript = "The forklift needs a battery swap.",
            AudioUrl = null,
            PassageWithBlanks = "The forklift needs a battery {{gap1}}.",
            Gaps = gapDtos,
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    [Fact]
    public async Task ListeningFillInBlanks_AllCorrect_ReturnsFullScore()
    {
        var content = ListeningFillInBlanksContent(
            ("gap1", "swap", ["swap"], ["swap", "charge", "check", "test"]),
            ("gap2", "delayed", ["delayed"], ["delayed", "cancelled", "delivered", "returned"]));
        var submitted = FillBlanksSubmitted(("gap1", "swap"), ("gap2", "delayed"));

        var request = MakeRequest(content, submitted, "listening_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.Score.Should().Be(2);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeTrue();
        result.ItemResults.Should().AllSatisfy(i => i.IsCorrect.Should().BeTrue());
    }

    [Fact]
    public async Task ListeningFillInBlanks_OneWrong_ReturnsPartialScore()
    {
        var content = ListeningFillInBlanksContent(
            ("gap1", "swap", ["swap"], ["swap", "charge", "check", "test"]),
            ("gap2", "delayed", ["delayed"], ["delayed", "cancelled", "delivered", "returned"]));
        var submitted = FillBlanksSubmitted(("gap1", "charge"), ("gap2", "delayed"));

        var request = MakeRequest(content, submitted, "listening_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeFalse();
        result.ItemResults.Should().ContainSingle(i => i.IsCorrect);
    }

    [Fact]
    public async Task ListeningFillInBlanks_AcceptsAlternativeAnswer()
    {
        var content = ListeningFillInBlanksContent(
            ("gap1", "swap", ["swap", "replacement"], ["swap", "charge", "check", "test"]));
        var submitted = FillBlanksSubmitted(("gap1", "replacement"));

        var request = MakeRequest(content, submitted, "listening_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ListeningFillInBlanks_NormalizesCaseAndWhitespace()
    {
        var content = ListeningFillInBlanksContent(("gap1", "Swap", ["Swap"], ["Swap", "Charge"]));
        var submitted = FillBlanksSubmitted(("gap1", "  swap  "));

        var request = MakeRequest(content, submitted, "listening_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ListeningFillInBlanks_MissingAnswer_HandledSafely()
    {
        var content = ListeningFillInBlanksContent(("gap1", "swap", ["swap"], ["swap", "charge"]));
        var submitted = FillBlanksSubmitted();

        var request = MakeRequest(content, submitted, "listening_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.ItemResults.Single().IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(1);
    }

    // ── reorder_paragraphs ────────────────────────────────────────────────────

    private static string ReorderParagraphsContent(
        string[] correctOrder,
        params (string id, string text)[] items)
    {
        var content = new LinguaCoach.Application.Activity.ReorderParagraphsContent
        {
            Items = items.Select(i => new LinguaCoach.Application.Activity.ReorderParagraphsItemDto { Id = i.id, Text = i.text }).ToList(),
            CorrectOrder = correctOrder.ToList(),
            Explanation = "This is the logical order because each paragraph follows from the previous.",
            ItemExplanations = correctOrder
                .Select((id, idx) => (id, explanation: $"Paragraph {idx + 1} explanation."))
                .ToDictionary(x => x.id, x => x.explanation),
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    private static string ReorderSubmitted(params string[] orderedIds) =>
        JsonSerializer.Serialize(new ReorderParagraphsSubmittedAnswer { OrderedIds = orderedIds.ToList() }, JsonOptions);

    [Fact]
    public async Task ReorderParagraphs_CorrectOrder_ReturnsFullScore()
    {
        var content = ReorderParagraphsContent(
            ["p1", "p2", "p3", "p4"],
            ("p1", "First paragraph."), ("p2", "Second paragraph."), ("p3", "Third paragraph."), ("p4", "Fourth paragraph."));
        var submitted = ReorderSubmitted("p1", "p2", "p3", "p4");

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "reorder_paragraphs"), default);

        result.Score.Should().Be(4);
        result.MaxScore.Should().Be(4);
        result.Passed.Should().BeTrue();
        result.ItemResults.Should().AllSatisfy(i => i.IsCorrect.Should().BeTrue());
    }

    [Fact]
    public async Task ReorderParagraphs_OneMisplaced_ReturnsPartialScore()
    {
        var content = ReorderParagraphsContent(
            ["p1", "p2", "p3", "p4"],
            ("p1", "First."), ("p2", "Second."), ("p3", "Third."), ("p4", "Fourth."));
        var submitted = ReorderSubmitted("p1", "p3", "p2", "p4"); // p2/p3 swapped

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "reorder_paragraphs"), default);

        result.Score.Should().Be(2); // p1 and p4 correct
        result.MaxScore.Should().Be(4);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ReorderParagraphs_DuplicateIds_DeduplicatesAndEvaluates()
    {
        var content = ReorderParagraphsContent(
            ["p1", "p2", "p3"],
            ("p1", "First."), ("p2", "Second."), ("p3", "Third."));
        var submitted = ReorderSubmitted("p1", "p1", "p2"); // duplicate p1

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "reorder_paragraphs"), default);

        // After dedup: [p1, p2] — position 1 (p1) correct, position 2 (p2 vs expected p2) correct, position 3 missing
        result.ItemResults.Should().HaveCount(3);
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderParagraphs_EmptySubmission_ReturnsZeroScore()
    {
        var content = ReorderParagraphsContent(
            ["p1", "p2"],
            ("p1", "First."), ("p2", "Second."));
        var submitted = ReorderSubmitted(); // nothing submitted

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "reorder_paragraphs"), default);

        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderParagraphs_AllWrong_ReturnsZeroScore()
    {
        var content = ReorderParagraphsContent(
            ["p1", "p2", "p3"],
            ("p1", "First."), ("p2", "Second."), ("p3", "Third."));
        var submitted = ReorderSubmitted("p3", "p1", "p2"); // completely wrong

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "reorder_paragraphs"), default);

        result.Score.Should().Be(0);
        result.Passed.Should().BeFalse();
    }

    // ── reading_writing_fill_in_blanks ────────────────────────────────────────

    [Fact]
    public async Task ReadingWritingFillInBlanks_AllCorrect_ReturnsFullScore()
    {
        var content = ReadingFillInBlanksContent(
            ("gap1", "acquisition", ["acquisition", "acquirement", "acquiring"]),
            ("gap2", "significant", ["significant", "signify", "significance"]));
        var submitted = FillBlanksSubmitted(("gap1", "acquisition"), ("gap2", "significant"));

        var request = MakeRequest(content, submitted, "reading_writing_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.Score.Should().Be(2);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ReadingWritingFillInBlanks_OneWrong_ReturnsPartialScore()
    {
        var content = ReadingFillInBlanksContent(
            ("gap1", "acquisition", ["acquisition", "acquirement", "acquiring"]),
            ("gap2", "significant", ["significant", "signify", "significance"]));
        var submitted = FillBlanksSubmitted(("gap1", "acquirement"), ("gap2", "significant"));

        var request = MakeRequest(content, submitted, "reading_writing_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.Score.Should().Be(1);
        result.ItemResults.Single(r => r.ItemKey == "gap1").IsCorrect.Should().BeFalse();
        result.ItemResults.Single(r => r.ItemKey == "gap2").IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ReadingWritingFillInBlanks_NormalizesCase()
    {
        var content = ReadingFillInBlanksContent(("gap1", "Significant", ["Significant", "signify"]));
        var submitted = FillBlanksSubmitted(("gap1", "significant"));

        var request = MakeRequest(content, submitted, "reading_writing_fill_in_blanks");
        var result = await _sut.EvaluateAsync(request, default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    // ── write_from_dictation ──────────────────────────────────────────────────

    private static string DictationContent(params (string id, string answer, string[]? accepted)[] items)
    {
        var content = new WriteFromDictationContent
        {
            Items = items.Select(i => new WriteFromDictationItem
            {
                Id = i.id,
                AudioScript = i.answer,
                AudioUrl = null,
                Answer = i.answer,
                AcceptedAnswers = i.accepted?.ToList(),
            }).ToList()
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    private static string DictationSubmitted(params (string itemId, string? text)[] items)
    {
        var dto = new WriteFromDictationSubmittedAnswer
        {
            Items = items.Select(i => new WriteFromDictationSubmittedItem { ItemId = i.itemId, SubmittedText = i.text }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    [Fact]
    public async Task Dictation_AllCorrect_ReturnsFullScoreAndPassed()
    {
        var content = DictationContent(
            ("item1", "The meeting starts at nine.", null),
            ("item2", "Please send the report.", null));
        var submitted = DictationSubmitted(
            ("item1", "The meeting starts at nine."),
            ("item2", "Please send the report."));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "write_from_dictation"), default);

        result.Score.Should().Be(2);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeTrue();
        result.ItemResults.Should().OnlyContain(r => r.IsCorrect);
    }

    [Fact]
    public async Task Dictation_OneWrong_ReturnsPerItemDetail()
    {
        var content = DictationContent(
            ("item1", "The meeting starts at nine.", null),
            ("item2", "Please send the report.", null));
        var submitted = DictationSubmitted(
            ("item1", "The meeting starts at nine."),
            ("item2", "Please send the email."));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "write_from_dictation"), default);

        result.Score.Should().Be(1);
        var wrong = result.ItemResults.Single(r => r.ItemKey == "item2");
        wrong.IsCorrect.Should().BeFalse();
        wrong.CorrectAnswer.Should().Be("Please send the report.");
    }

    [Fact]
    public async Task Dictation_CaseInsensitiveAndTrimmed_Matches()
    {
        var content = DictationContent(("item1", "The Meeting Starts At Nine.", null));
        var submitted = DictationSubmitted(("item1", "  the meeting starts at nine  "));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "write_from_dictation"), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task Dictation_AcceptedAlternative_Matches()
    {
        var content = DictationContent(("item1", "We will meet at 9.", ["We will meet at nine.", "We will meet at 9."]));
        var submitted = DictationSubmitted(("item1", "we will meet at nine"));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "write_from_dictation"), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task Dictation_MissingSubmission_IsIncorrectNotError()
    {
        var content = DictationContent(("item1", "Hello team.", null), ("item2", "Goodbye.", null));
        var submitted = DictationSubmitted(("item1", "Hello team."));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "write_from_dictation"), default);

        result.Score.Should().Be(1);
        result.ItemResults.Single(r => r.ItemKey == "item2").IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task Dictation_EmptyText_IsIncorrect()
    {
        var content = DictationContent(("item1", "Hello team.", null));
        var submitted = DictationSubmitted(("item1", "   "));

        var result = await _sut.EvaluateAsync(MakeRequest(content, submitted, "write_from_dictation"), default);

        result.ItemResults.Single().IsCorrect.Should().BeFalse();
    }

    // ── answer_short_question ─────────────────────────────────────────────────

    private static string AsqContent(params (string id, string question, string expected, string[]? accepted)[] items)
    {
        var content = new AnswerShortQuestionContent
        {
            Items = items.Select(i => new AnswerShortQuestionItem
            {
                Id = i.id,
                Question = i.question,
                ExpectedAnswer = i.expected,
                AcceptedAnswers = i.accepted?.ToList(),
            }).ToList()
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    private static string AsqSubmitted(params (string itemId, string? answerText)[] items)
    {
        var dto = new AnswerShortQuestionSubmittedAnswer
        {
            Items = items.Select(i => new AnswerShortQuestionSubmittedItem
            {
                ItemId = i.itemId,
                AnswerText = i.answerText,
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static PatternEvaluationRequest AsqRequest(string contentJson, string submittedJson) =>
        MakeRequest(contentJson, submittedJson, "answer_short_question");

    // ── read_aloud helpers ────────────────────────────────────────────────────

    private static string RaContent(params (string id, string text)[] items)
    {
        var content = new ReadAloudContent
        {
            Items = items.Select(i => new ReadAloudItem
            {
                Id = i.id,
                Text = i.text,
                ExpectedText = i.text,
            }).ToList()
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    private static string RaSubmitted(params (string itemId, string? answerText)[] items)
    {
        var dto = new ReadAloudSubmittedAnswer
        {
            Items = items.Select(i => new ReadAloudSubmittedItem
            {
                ItemId = i.itemId,
                AnswerText = i.answerText,
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static PatternEvaluationRequest RaRequest(string contentJson, string submittedJson) =>
        MakeRequest(contentJson, submittedJson, "read_aloud");

    [Fact]
    public async Task AnswerShortQuestion_AllCorrect_ReturnsFullScore()
    {
        var content = AsqContent(
            ("q1", "Where is the meeting?", "room 3", null),
            ("q2", "Who is presenting?", "Sarah", null));
        var submitted = AsqSubmitted(("q1", "room 3"), ("q2", "Sarah"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.Score.Should().Be(2);
        result.MaxScore.Should().Be(2);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.ItemResults.Should().OnlyContain(r => r.IsCorrect);
    }

    [Fact]
    public async Task AnswerShortQuestion_ContainsMatch_IsAccepted()
    {
        // Student says "in room 3" — expected "room 3" — contains match should pass
        var content = AsqContent(("q1", "Where is the meeting?", "room 3", null));
        var submitted = AsqSubmitted(("q1", "in room 3"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerShortQuestion_AcceptedAlternative_IsAccepted()
    {
        var content = AsqContent(("q1", "Who leads it?", "Sarah", ["Sarah", "Ms Smith"]));
        var submitted = AsqSubmitted(("q1", "Ms Smith"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerShortQuestion_CaseInsensitive_Matches()
    {
        var content = AsqContent(("q1", "When is it?", "Monday", null));
        var submitted = AsqSubmitted(("q1", "MONDAY"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerShortQuestion_OneWrong_ReturnsPartialScore()
    {
        var content = AsqContent(
            ("q1", "Where is the meeting?", "room 3", null),
            ("q2", "Who is presenting?", "Sarah", null));
        var submitted = AsqSubmitted(("q1", "room 3"), ("q2", "James"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(2);
        result.ItemResults.Single(r => r.ItemKey == "q2").IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task AnswerShortQuestion_EmptyAnswer_IsIncorrect()
    {
        var content = AsqContent(("q1", "Where is the meeting?", "room 3", null));
        var submitted = AsqSubmitted(("q1", "   "));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Fact]
    public async Task AnswerShortQuestion_MissingAnswer_IsIncorrectNotError()
    {
        var content = AsqContent(
            ("q1", "Where?", "room 3", null),
            ("q2", "Who?", "Sarah", null));
        var submitted = AsqSubmitted(("q1", "room 3")); // q2 missing

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.ItemResults.Single(r => r.ItemKey == "q2").IsCorrect.Should().BeFalse();
        result.ItemResults.Single(r => r.ItemKey == "q2").StudentAnswer.Should().BeNull();
    }

    [Fact]
    public async Task AnswerShortQuestion_AllWrong_IsCompletedNotPassed()
    {
        var content = AsqContent(
            ("q1", "Where?", "room 3", null),
            ("q2", "Who?", "Sarah", null),
            ("q3", "When?", "Monday", null));
        var submitted = AsqSubmitted(("q1", "wrong"), ("q2", "wrong"), ("q3", "wrong"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.Score.Should().Be(0);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerShortQuestion_UnknownItem_NotScoredAndReported()
    {
        var content = AsqContent(("q1", "Where?", "room 3", null));
        var submitted = AsqSubmitted(("q1", "room 3"), ("q99", "extra"));

        var result = await _sut.EvaluateAsync(AsqRequest(content, submitted), default);

        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        // q99 extra item has MaxScore=0 so doesn't affect score
        var extra = result.ItemResults.FirstOrDefault(r => r.ItemKey == "q99");
        extra.Should().NotBeNull();
        extra!.MaxScore.Should().Be(0);
    }

    [Fact]
    public async Task AnswerShortQuestion_EmptySubmissionJson_ReturnsZeroScore()
    {
        var content = AsqContent(("q1", "Where?", "room 3", null));

        var result = await _sut.EvaluateAsync(AsqRequest(content, ""), default);

        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(1);
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerShortQuestion_StagedContent_IsUnwrappedCorrectly()
    {
        var innerContent = new AnswerShortQuestionContent
        {
            Items = [new AnswerShortQuestionItem { Id = "q1", Question = "Where?", ExpectedAnswer = "room 3" }]
        };
        var staged = new
        {
            schemaVersion = "module_stage_v1",
            learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
            practiceContent = new { instructions = "I", scenario = (string?)null, task = "T", exerciseData = innerContent },
            feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
        };
        var contentJson = JsonSerializer.Serialize(staged, JsonOptions);
        var submitted = AsqSubmitted(("q1", "room 3"));

        var result = await _sut.EvaluateAsync(AsqRequest(contentJson, submitted), default);

        result.Score.Should().Be(1);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerShortQuestion_60PercentThreshold_PassFail()
    {
        // 3 items: need ≥2 correct to pass (66.7% ≥ 60%)
        var content = AsqContent(
            ("q1", "A?", "yes", null),
            ("q2", "B?", "no", null),
            ("q3", "C?", "maybe", null));
        var twoCorrect = AsqSubmitted(("q1", "yes"), ("q2", "no"), ("q3", "wrong"));
        var oneCorrect = AsqSubmitted(("q1", "yes"), ("q2", "wrong"), ("q3", "wrong"));

        var pass = await _sut.EvaluateAsync(AsqRequest(content, twoCorrect), default);
        var fail = await _sut.EvaluateAsync(AsqRequest(content, oneCorrect), default);

        pass.Passed.Should().BeTrue();
        fail.Passed.Should().BeFalse();
    }

    // ── ReadAloud tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAloud_PerfectTranscript_IsCorrect()
    {
        var text = "Please send the updated report by end of day.";
        var content = RaContent(("t1", text));
        var submitted = RaSubmitted(("t1", text));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
        result.Score.Should().BeGreaterThanOrEqualTo(0.9);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ReadAloud_HighWordOverlap_IsCorrect()
    {
        var content = RaContent(("t1", "Please send the updated report by end of day."));
        // Missing "updated" and "end" — still >60% overlap
        var submitted = RaSubmitted(("t1", "Please send the report by of day."));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ReadAloud_LowWordOverlap_IsIncorrect()
    {
        var content = RaContent(("t1", "The quarterly budget meeting has been postponed until Thursday."));
        // Very different words — well below 60%
        var submitted = RaSubmitted(("t1", "hello world"));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAloud_EmptyAnswer_IsIncorrect()
    {
        var content = RaContent(("t1", "All staff must complete the training by Friday."));
        var submitted = RaSubmitted(("t1", ""));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeFalse();
        result.ItemResults[0].Score.Should().Be(0);
    }

    [Fact]
    public async Task ReadAloud_MissingAnswer_IsIncorrectNotError()
    {
        var content = RaContent(("t1", "Please confirm your attendance."));
        var submitted = RaSubmitted();

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.Completed.Should().BeTrue();
        result.ItemResults[0].IsCorrect.Should().BeFalse();
        result.ItemResults[0].Score.Should().Be(0);
    }

    [Fact]
    public async Task ReadAloud_MultipleItems_ScoresSeparately()
    {
        var content = RaContent(
            ("t1", "Please send the report by Friday."),
            ("t2", "The meeting has been rescheduled to Tuesday."));
        // t1: good overlap, t2: no overlap
        var submitted = RaSubmitted(
            ("t1", "Please send the report by Friday."),
            ("t2", "hello world goodbye"));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.ItemResults.Should().HaveCount(2);
        result.ItemResults.First(r => r.ItemKey == "t1").IsCorrect.Should().BeTrue();
        result.ItemResults.First(r => r.ItemKey == "t2").IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAloud_CaseInsensitive_Matches()
    {
        var content = RaContent(("t1", "Please Submit The Form Today."));
        var submitted = RaSubmitted(("t1", "please submit the form today."));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.ItemResults[0].IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task ReadAloud_StagedContent_IsUnwrappedCorrectly()
    {
        var innerContent = new ReadAloudContent
        {
            Items = [new ReadAloudItem { Id = "t1", Text = "Send the report.", ExpectedText = "Send the report." }]
        };
        var staged = new
        {
            schemaVersion = "module_stage_v1",
            learnContent = new { teachingTitle = "T", explanation = "E", keyPoints = Array.Empty<string>(), examples = Array.Empty<object>(), strategy = "S", commonMistakes = Array.Empty<string>(), sourceLanguageSupport = (string?)null },
            practiceContent = new { instructions = "I", scenario = (string?)null, task = "T", exerciseData = innerContent },
            feedbackPlan = new { evaluationCriteria = Array.Empty<string>(), rubric = Array.Empty<object>(), feedbackFocus = "F", successCriteria = Array.Empty<string>() },
        };
        var contentJson = JsonSerializer.Serialize(staged, JsonOptions);
        var submitted = RaSubmitted(("t1", "Send the report."));

        var result = await _sut.EvaluateAsync(RaRequest(contentJson, submitted), default);

        result.Score.Should().BeGreaterThanOrEqualTo(0.9);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ReadAloud_60PercentThreshold_PassFail()
    {
        // 2 items: need both ≥60% overlap individually, and overall ≥60%
        var content = RaContent(
            ("t1", "Please send the updated report by end of day."),
            ("t2", "The quarterly budget meeting has been postponed."));
        // t1 good, t2 poor → average may be below 60
        var submitted = RaSubmitted(
            ("t1", "Please send the updated report by end of day."),
            ("t2", "hello world"));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        // t1 passes, t2 fails — 1/2 correct = 50% → not passed overall
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAloud_UnknownItem_NotScoredAndReported()
    {
        var content = RaContent(("t1", "Send the report."));
        var submitted = RaSubmitted(("t1", "Send the report."), ("t99", "extra text"));

        var result = await _sut.EvaluateAsync(RaRequest(content, submitted), default);

        result.MaxScore.Should().Be(1);
        var extra = result.ItemResults.FirstOrDefault(r => r.ItemKey == "t99");
        extra.Should().NotBeNull();
        extra!.MaxScore.Should().Be(0);
    }

    // ── repeat_sentence helpers ───────────────────────────────────────────────

    private static string RsContent(params (string id, string sentence)[] items)
    {
        var content = new RepeatSentenceContent
        {
            Items = items.Select(i => new RepeatSentenceItem
            {
                Id = i.id,
                Sentence = i.sentence,
                AudioScript = i.sentence,
            }).ToList()
        };
        return JsonSerializer.Serialize(content, JsonOptions);
    }

    private static string RsSubmitted(params (string itemId, string? answerText)[] items)
    {
        var dto = new RepeatSentenceSubmittedAnswer
        {
            Items = items.Select(i => new RepeatSentenceSubmittedItem
            {
                ItemId = i.itemId,
                AnswerText = i.answerText,
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static PatternEvaluationRequest RsRequest(string contentJson, string submittedJson) =>
        MakeRequest(contentJson, submittedJson, "repeat_sentence");

    // ── RepeatSentence tests ──────────────────────────────────────────────────

    [Fact]
    public async Task RepeatSentence_PerfectTranscript_IsCorrect()
    {
        var content = RsContent(("s1", "Please send the updated report by end of day."));
        var submitted = RsSubmitted(("s1", "Please send the updated report by end of day."));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_HighWordOverlap_IsCorrect()
    {
        // 5 of 7 words = ~71% — above 60% threshold
        var content = RsContent(("s1", "The meeting is scheduled for Monday morning."));
        var submitted = RsSubmitted(("s1", "meeting is scheduled for Monday morning"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_LowWordOverlap_IsIncorrect()
    {
        var content = RsContent(("s1", "Please confirm your attendance by Friday afternoon."));
        var submitted = RsSubmitted(("s1", "yes ok"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeFalse();
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task RepeatSentence_EmptyAnswer_IsIncorrect()
    {
        var content = RsContent(("s1", "Can you send me the file?"));
        var submitted = RsSubmitted(("s1", ""));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeFalse();
        result.ItemResults.Single().Feedback.Should().Contain("No transcript");
    }

    [Fact]
    public async Task RepeatSentence_MissingAnswer_IsIncorrectNotError()
    {
        var content = RsContent(("s1", "Can you send me the file?"));
        var submitted = RsSubmitted();

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeFalse();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_CaseInsensitive_Matches()
    {
        var content = RsContent(("s1", "Send the report today."));
        var submitted = RsSubmitted(("s1", "SEND THE REPORT TODAY"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_PunctuationTolerant_Matches()
    {
        var content = RsContent(("s1", "Please review the document, then reply."));
        var submitted = RsSubmitted(("s1", "please review the document then reply"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_MultipleItems_ScoresSeparately()
    {
        var content = RsContent(
            ("s1", "I need the report by five."),
            ("s2", "The client called this morning."));
        var submitted = RsSubmitted(
            ("s1", "I need the report by five."),
            ("s2", "wrong answer completely off"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Should().HaveCount(2);
        result.ItemResults.First(r => r.ItemKey == "s1").IsCorrect.Should().BeTrue();
        result.ItemResults.First(r => r.ItemKey == "s2").IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task RepeatSentence_MissingWordsFeedback_Listed()
    {
        var content = RsContent(("s1", "The project deadline is next Friday."));
        var submitted = RsSubmitted(("s1", "project is next"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.ItemResults.Single().Feedback.Should().Contain("Missing");
    }

    [Fact]
    public async Task RepeatSentence_ExtraWordsFeedback_Listed()
    {
        var content = RsContent(("s1", "Call me back later."));
        var submitted = RsSubmitted(("s1", "call me back later please immediately now"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        // 4/4 expected words matched so it passes, extra words noted if below threshold or just pass
        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_StagedContent_IsUnwrappedCorrectly()
    {
        var inner = new RepeatSentenceContent
        {
            Items = [new RepeatSentenceItem { Id = "s1", Sentence = "I will send it now." }]
        };
        var staged = new
        {
            schemaVersion = "module_stage_v1",
            practiceContent = new
            {
                exerciseData = inner
            }
        };
        var contentJson = JsonSerializer.Serialize(staged, JsonOptions);
        var submitted = RsSubmitted(("s1", "I will send it now."));

        var result = await _sut.EvaluateAsync(RsRequest(contentJson, submitted), default);

        result.ItemResults.Single().IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatSentence_UnknownItem_NotScoredAndReported()
    {
        var content = RsContent(("s1", "Thank you for your help."));
        var submitted = RsSubmitted(("s1", "Thank you for your help."), ("s99", "extra"));

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.MaxScore.Should().Be(1);
        var extra = result.ItemResults.FirstOrDefault(r => r.ItemKey == "s99");
        extra.Should().NotBeNull();
        extra!.MaxScore.Should().Be(0);
    }

    [Fact]
    public async Task RepeatSentence_EmptyContent_ReturnsCompleted()
    {
        var content = RsContent();
        var submitted = RsSubmitted();

        var result = await _sut.EvaluateAsync(RsRequest(content, submitted), default);

        result.Completed.Should().BeTrue();
        result.MaxScore.Should().Be(0);
        result.Passed.Should().BeTrue();
    }
}
