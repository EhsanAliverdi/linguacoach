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
}
