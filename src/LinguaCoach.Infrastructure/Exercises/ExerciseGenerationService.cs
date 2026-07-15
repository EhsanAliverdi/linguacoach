using System.Text.Json;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase H4 — deterministic "Generate Activity" composer for <see cref="IGenerateActivityFromLessonHandler"/>.
/// Builds a pending-review <see cref="Exercise"/> draft from the fields of the resources linked to
/// an existing Lesson — no AI provider call, matching Phase H3's <c>LessonGenerationService</c>
/// decision (no existing AI service in this codebase generates a scored practice exercise from
/// source text). Never modifies the resources or Lesson it reads from, never creates a Module row,
/// never assigns anything to a student.
///
/// Phase 2 (2026-07-15) — the direct "generate from Resource Bank items with no Lesson" entry
/// point (<c>IGenerateActivityFromResourcesHandler</c>) was removed; every Exercise now requires a
/// Lesson.
///
/// Supported <c>ActivityType</c>s:
/// <list type="bullet">
/// <item><description><c>gap_fill</c> — Vocabulary/Grammar only. Shows the resource's own
/// definition/description, asks the student to type the term. Deterministically scored
/// (text_normalized).</description></item>
/// <item><description><c>multiple_choice_single</c> — Vocabulary/Grammar only. Asks "what does X
/// mean", with distractor options pulled from sibling published resources of the same type
/// already in the bank. Deterministically scored (single_choice). Requires at least one usable
/// distractor — generation is rejected (not silently degraded to a single-option "choice") when
/// none exist.</description></item>
/// <item><description><c>short_answer</c> — ReadingReference/ReadingPassage only. Open-ended
/// comprehension prompt over the resource's own excerpt/summary text, explicitly marked
/// <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> — reading comprehension can't be
/// deterministically graded from thin metadata, so this is honestly left ungraded rather than
/// faking a score.</description></item>
/// <item><description><c>reading_fill_in_blanks</c> — ReadingReference/ReadingPassage only
/// (Phase K16). Deterministic cloze over the resource's own excerpt/passage text — blanks out up
/// to 4 content words (length &gt;= 5, alphabetic), deterministically scored per-blank
/// (text_normalized), same "never AI-supplied correct answer" shape as gap_fill. Rejected when the
/// source text doesn't have enough distinct content words to build a meaningful cloze (fewer than
/// 2) — same "reject rather than degrade" discipline as multiple_choice_single's distractor
/// check.</description></item>
/// <item><description><c>email_reply</c> / <c>open_writing_task</c> / <c>write_essay</c> — Writing
/// resources only (Phase K17). Shows the resource's own PromptText verbatim as the task, honestly
/// marked <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> — same open-ended shape as
/// short_answer, just sourced from a Writing resource instead of a Reading one.</description></item>
/// <item><description><c>summarize_written_text</c> — ReadingReference/ReadingPassage only (Phase
/// K17). Writing-skill but Reading-resource-sourced — same shape as short_answer, asks for a
/// summary instead of an answer to a specific question.</description></item>
/// <item><description><c>listening_fill_in_blanks</c> — Listening resources only (Phase K17).
/// Same shared cloze algorithm as reading_fill_in_blanks, sourced from the resource's own
/// transcript.</description></item>
/// <item><description><c>spoken_response_from_prompt</c> / <c>respond_to_situation</c> /
/// <c>answer_short_question</c> / <c>speaking_roleplay_turn</c> / <c>read_aloud</c> — Speaking
/// resources only (Phase K18). Shows the resource's own PromptText verbatim, student responds via
/// the stock "speakingResponse" Form.io component, honestly marked
/// <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> — real audio scoring isn't
/// wired into the bank-first pipeline yet (see ComposeSpeakingPrompt's doc comment).</description></item>
/// <item><description><c>summarize_spoken_text</c> / <c>retell_lecture</c> /
/// <c>summarize_group_discussion</c> — Listening resources only (Phase K18). Reuse
/// ComposeWritingPrompt/ComposeSpeakingPrompt unchanged against the resource's own transcript —
/// no audio playback involved (none exists in the bank-first pipeline yet), just the transcript
/// text shown for the student to summarize/retell.</description></item>
/// <item><description><c>phrase_match</c> — Vocabulary/Grammar only (Phase K16). Decomposes
/// "matching" into N single_choice sub-questions using sibling resources of the same type — see
/// <see cref="ActivityTypePhraseMatch"/>'s doc comment.</description></item>
/// <item><description><c>reorder_paragraphs</c> — ReadingPassage only in practice (Phase K16).
/// Stock Form.io datagrid+reorder pattern, scored via
/// <see cref="ScoringRuleKinds.OrderedSequence"/> — see
/// <see cref="ActivityTypeReorderParagraphs"/>'s doc comment, including the one thing this session
/// could not verify without a browser.</description></item>
/// </list>
/// <see cref="Exercise.ScoringRulesJson"/> is serialized straight from the shared
/// <see cref="ScoringRulesDocument"/>/<see cref="ComponentScoringRule"/> types already used by
/// placement/onboarding/reorder_paragraphs scoring, so a future runtime integration can reuse
/// <see cref="LinguaCoach.Application.FormIo.ComponentAnswerScorer"/> as-is.
/// </summary>
public sealed class ActivityGenerationService : IGenerateActivityFromLessonHandler
{
    private const string GenerationProvider = "Deterministic";
    private const string GenerationModel = "activity-draft-composer-v1";
    private const int MaxDistractors = 3;

    public const string ActivityTypeGapFill = "gap_fill";
    public const string ActivityTypeMultipleChoiceSingle = "multiple_choice_single";
    public const string ActivityTypeShortAnswer = "short_answer";
    public const string ActivityTypeReadingFillInBlanks = "reading_fill_in_blanks";

    /// <summary>Phase K18 — same resource-type bucket as <see cref="ActivityTypeReadingFillInBlanks"/>,
    /// "choose" instead of "type" variant — see <see cref="ComposeReadingWritingFillInBlanksAsync"/>.</summary>
    public const string ActivityTypeReadingWritingFillInBlanks = "reading_writing_fill_in_blanks";

    /// <summary>Phase K17 — AI-only activity type, no deterministic composer exists (or can
    /// exist) for it: unlike gap_fill/multiple_choice_single, ReadingReference/ReadingPassage
    /// resources have no single "the answer is X" field to derive a correct answer from, so
    /// building a real comprehension question requires the AI to judge the correct answer from
    /// the passage text itself — a deliberate, documented, scoped exception to this project's
    /// "AI never supplies the correct answer" rule (confirmed via AskUserQuestion). The existing
    /// PendingReview admin-approval gate is the safety net, same as every other generated
    /// Exercise. See <see cref="LinguaCoach.Infrastructure.Exercises.AiExerciseGenerationService"/>.</summary>
    public const string ActivityTypeReadingMultipleChoiceSingle = "reading_multiple_choice_single";

    /// <summary>Phase K17 — same AI-only rationale as
    /// <see cref="ActivityTypeReadingMultipleChoiceSingle"/>, multi-select variant.</summary>
    public const string ActivityTypeReadingMultipleChoiceMulti = "reading_multiple_choice_multi";

    /// <summary>Phase K17 — Writing resources only. Deterministic — like short_answer, this is
    /// open-ended and honestly marked <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/>,
    /// so there's no correctness risk to route through AI for; the prompt shown to the student is
    /// always the resource's own PromptText verbatim.</summary>
    public const string ActivityTypeEmailReply = "email_reply";
    public const string ActivityTypeOpenWritingTask = "open_writing_task";
    public const string ActivityTypeWriteEssay = "write_essay";

    /// <summary>Phase K20 — same Writing-resource, deterministic, unscored shape as
    /// email_reply/open_writing_task/write_essay, reusing ComposeWritingPrompt unchanged.
    /// Deliberately simplified from the catalog's original "multi-turn chat" ambition
    /// (RendererKey historically "chat_reply") to a single-turn written reply: no multi-turn
    /// Form.io component exists in this codebase ("chat_reply" was never an allow-listed
    /// component type — confirmed zero implementation existed anywhere), and building one is a
    /// genuinely separate UI feature, not a composer. A single realistic chat message shown, one
    /// written reply — honestly labeled as simplified rather than silently passed off as the
    /// original multi-turn design.</summary>
    public const string ActivityTypeTeamsChatSimulation = "teams_chat_simulation";

    /// <summary>Phase K17 — Writing-skill but Reading-resource-sourced (ReadingReference/
    /// ReadingPassage), unlike the 3 constants above which are Writing-resource-sourced. Same
    /// deterministic, unscored shape as short_answer — asks for a summary instead of an answer to
    /// a specific question.</summary>
    public const string ActivityTypeSummarizeWrittenText = "summarize_written_text";

    /// <summary>Phase K17 — deterministic, Listening resources only. Same shared cloze algorithm
    /// as <see cref="ActivityTypeReadingFillInBlanks"/>, sourced from the resource's own
    /// transcript.</summary>
    public const string ActivityTypeListeningFillInBlanks = "listening_fill_in_blanks";

    /// <summary>Phase K17 — same AI-only rationale as
    /// <see cref="ActivityTypeReadingMultipleChoiceSingle"/>/<see cref="ActivityTypeReadingMultipleChoiceMulti"/>,
    /// sourced from a Listening resource's transcript instead of a Reading excerpt/passage.</summary>
    public const string ActivityTypeListeningMultipleChoiceSingle = "listening_multiple_choice_single";
    public const string ActivityTypeListeningMultipleChoiceMulti = "listening_multiple_choice_multi";

    /// <summary>Phase K17 — same AI-only rationale as the reading/listening MC types above: no
    /// fact field exists to derive "the correct summary" from, so AI judges it from the
    /// transcript. Listening resources only.</summary>
    public const string ActivityTypeHighlightCorrectSummary = "highlight_correct_summary";

    /// <summary>Phase K17 — unlike every other AI-routed comprehension type above, the correct
    /// answer here IS deterministic: a real word picked directly from the resource's own
    /// transcript, never AI-supplied. AI is only asked for plausible-but-wrong word distractors —
    /// same safe shape as multiple_choice_single (Vocabulary/Grammar), not the "AI supplies the
    /// answer" exception. Listening resources only.</summary>
    public const string ActivityTypeSelectMissingWord = "select_missing_word";

    /// <summary>Phase K21 — the correct answer is deterministic: N distinct content words from
    /// the resource's own transcript are rotated among each other's positions (word A takes word
    /// B's slot and vice versa), so every "wrong" word shown really is a real word from the same
    /// transcript, just in the wrong place — no AI call, no synthetic distractor text. The student
    /// hears the resource's own unaltered audio (streamed via
    /// <see cref="LinguaCoach.Api.Controllers.ActivityController.GetResourceAudio"/>, Phase K21's
    /// audio-serving bridge for the bank-first pipeline) and clicks the displayed words that don't
    /// match what they heard. Requires at least <see cref="MinHighlightAlteredWords"/> distinct
    /// eligible content words in the transcript — rejects rather than degrading (same discipline
    /// as BuildCloze) when the transcript is too short. Listening resources only.</summary>
    public const string ActivityTypeHighlightIncorrectWords = "highlight_incorrect_words";

    /// <summary>Phase K22 — Listening resources only, built on the Phase K21 audio-serving
    /// bridge. Splits the resource's own transcript into up to
    /// <see cref="MaxDictationSentences"/> distinct sentences and asks the student to type each
    /// one exactly as heard, deterministically scored per-sentence
    /// (<see cref="ScoringRuleKinds.TextNormalized"/> — same leniency precedent as BuildCloze's
    /// per-blank scoring). The student hears the resource's own unaltered full audio (one
    /// continuous track, same <c>audioPlayer</c> streamed via
    /// <see cref="LinguaCoach.Api.Controllers.ActivityController.GetResourceAudio"/> as
    /// highlight_incorrect_words) rather than per-sentence clips — there is no per-sentence audio
    /// segmentation infrastructure, and splitting the audio file itself is out of scope; the
    /// transcript's own sentence boundaries are used only to size the typed-answer blanks.
    /// Rejects rather than degrading when the transcript has fewer than
    /// <see cref="MinDictationSentences"/> usable sentences.</summary>
    public const string ActivityTypeWriteFromDictation = "write_from_dictation";

    /// <summary>Phase K22 — Speaking resources only, same honest unscored shape as
    /// <see cref="ComposeSpeakingPrompt"/>/<see cref="ActivityTypeReadAloud"/> (no real speech
    /// scoring exists in the bank-first pipeline for any Speaking type yet). Splits the
    /// resource's own PromptText into up to <see cref="MaxRepeatSentences"/> distinct sentences,
    /// one <c>speakingResponse</c> component per sentence, each showing the sentence text as the
    /// prompt to repeat aloud — the catalog's own description ("Hear or read a short sentence,
    /// then repeat it") already allows a text-only prompt, so this doesn't need the Listening
    /// audio bridge. Rejects rather than degrading when the source text has fewer than
    /// <see cref="MinRepeatSentences"/> usable sentences.</summary>
    public const string ActivityTypeRepeatSentence = "repeat_sentence";

    /// <summary>Phase K18 — deterministic, Speaking resources only. Shows the resource's own
    /// PromptText verbatim, student responds via the speakingResponse component, honestly
    /// unscored (RequiresManualOrAiEvaluation) — see ComposeSpeakingPrompt's doc comment for why
    /// real audio scoring isn't wired in yet.</summary>
    public const string ActivityTypeSpokenResponseFromPrompt = "spoken_response_from_prompt";
    public const string ActivityTypeRespondToSituation = "respond_to_situation";
    public const string ActivityTypeAnswerShortQuestion = "answer_short_question";
    public const string ActivityTypeSpeakingRoleplayTurn = "speaking_roleplay_turn";
    public const string ActivityTypeReadAloud = "read_aloud";

    /// <summary>Phase K20 — deterministic, Speaking resources only, and only when the resource
    /// has <see cref="LinguaCoach.Application.ResourceImport.SpeakingPromptContent.ImageUrl"/>
    /// set. Rejects (does not degrade) when absent — most existing Speaking resources have no
    /// image, since it was added specifically to unblock this type rather than being a
    /// universally-required field.</summary>
    public const string ActivityTypeDescribeImage = "describe_image";

    /// <summary>Phase K18 — deterministic, Listening resources only. Reuses
    /// <see cref="ComposeWritingPrompt"/>/<see cref="ComposeSpeakingPrompt"/> unchanged — these
    /// are resource-agnostic (they just show <c>primary.Body</c> verbatim), so no new compose
    /// logic is needed, only new instructions text and a Listening bucket entry.
    /// <c>summarize_spoken_text</c> asks for a written summary; <c>retell_lecture</c>/
    /// <c>summarize_group_discussion</c> ask for a spoken response — all honestly unscored, same
    /// as every other open-ended type.</summary>
    public const string ActivityTypeSummarizeSpokenText = "summarize_spoken_text";
    public const string ActivityTypeRetellLecture = "retell_lecture";
    public const string ActivityTypeSummarizeGroupDiscussion = "summarize_group_discussion";

    /// <summary>Phase K16 — Vocabulary/Grammar only. No <c>matching_pairs</c> scoring kind exists
    /// in <c>ComponentAnswerScorer</c> — rather than add one, this decomposes "matching"
    /// into N independent single_choice sub-questions (one radio per term), each offering every
    /// pulled term's own definition as an option. That reproduces genuine matching semantics
    /// (a definition used as the correct answer for one term is a live distractor for every other
    /// term in the same exercise) using only already-proven scoring infrastructure. Same
    /// distractor-pool pattern as multiple_choice_single (sibling resources of the same type,
    /// preferring the same CEFR level) but keeps each sibling's title too, since every pair needs
    /// both.</summary>
    public const string ActivityTypePhraseMatch = "phrase_match";

    /// <summary>Phase K16 — ReadingPassage only (needs genuine multi-paragraph text; a
    /// ReadingReference excerpt is too short). Uses the stock Form.io "datagrid" (reorder-enabled)
    /// pattern already proven working — see
    /// FormIoSchemaValidationServiceTests.DatagridWithReorder_AndValidNestedComponents_IsValid and
    /// ComponentAnswerScorer.ScoreOrderedSequence's own doc comment, both written for exactly this
    /// shape. Scored via <see cref="ScoringRuleKinds.OrderedSequence"/> (already implemented, one
    /// point per correctly-placed paragraph). The one thing this session cannot verify without a
    /// browser: whether the frontend's generic Form.io renderer actually pre-populates the
    /// datagrid's rows from the schema's <c>defaultValue</c> the way this composer assumes —
    /// flagged for manual verification, not silently trusted.</summary>
    public const string ActivityTypeReorderParagraphs = "reorder_paragraphs";

    /// <summary>Phase K19 — the one Exercise type sourced from the Lesson's own Body, not a
    /// Resource Bank row at all — decided this phase (previously an open K19 product question).
    /// Bypasses the normal resource-linking requirement in
    /// <see cref="HandleAsync(GenerateActivityFromLessonRequest, CancellationToken)"/> entirely
    /// (there is no primary resource to resolve), and is therefore not offered by
    /// <see cref="IGenerateActivityFromResourcesHandler"/> — it only exists via the
    /// Lesson-generation entry point. Not skill-gated like every other type (the Lesson picker
    /// treats this one specially, always offering it regardless of the Lesson's own skill, since
    /// reflection is meta to the Lesson content, not skill-specific).</summary>
    public const string ActivityTypeLessonReflection = "lesson_reflection";

    private const int MaxClozeBlanks = 4;
    private const int MinClozeWordLength = 5;
    private const int MaxPhraseMatchSiblings = 4;
    private const int MinPhraseMatchPairs = 2;
    private const int MaxReorderParagraphs = 6;
    private const int MinReorderParagraphs = 3;
    private const int MaxHighlightAlteredWords = 3;
    private const int MinHighlightAlteredWords = 2;
    private const int MaxDictationSentences = 3;
    private const int MinDictationSentences = 2;
    private const int MaxRepeatSentences = 5;
    private const int MinRepeatSentences = 3;
    private const int MinDictationSentenceLength = 10;

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _schemaValidator;

    public ActivityGenerationService(LinguaCoachDbContext db, IFormIoSchemaValidationService schemaValidator)
    {
        _db = db;
        _schemaValidator = schemaValidator;
    }

    private readonly record struct ResolvedResource(
        ExerciseResourceLinkInput Input, PublishedResourceType Type, LessonResourceRole Role, LessonResourceSnapshot Snapshot);

    public async Task<GenerateExerciseResult> HandleAsync(
        GenerateActivityFromLessonRequest request, CancellationToken ct = default)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == request.LessonId, ct)
            ?? throw new ExerciseValidationException($"Lesson '{request.LessonId}' was not found.");

        // Phase K19 — lesson_reflection has no primary resource at all (see its constant's doc
        // comment), so it bypasses the "must have linked resources" requirement below entirely.
        if (string.Equals(request.RequestedActivityType?.Trim(), ActivityTypeLessonReflection, StringComparison.OrdinalIgnoreCase))
            return await ComposeAndSaveLessonReflectionAsync(lesson, request.Title, request.Notes, request.CreatedByUserId, ct);

        var lessonLinks = await _db.LessonResourceLinks
            .Where(l => l.LessonId == lesson.Id).ToListAsync(ct);
        if (lessonLinks.Count == 0)
            throw new ExerciseValidationException(
                "This Lesson has no linked resources to generate an Activity from.");

        var resolved = new List<ResolvedResource>();
        foreach (var link in lessonLinks)
        {
            var snapshot = await LessonResourceLookup.FindAsync(_db, link.ResourceType, link.ResourceId, ct)
                ?? throw new ExerciseValidationException(
                    $"Resource '{link.ResourceType}:{link.ResourceId}' linked to this Lesson is no longer available in the published Resource Bank.");

            resolved.Add(new ResolvedResource(
                new ExerciseResourceLinkInput(link.ResourceType.ToString(), link.ResourceId, link.Role.ToString()),
                link.ResourceType, link.Role, snapshot));
        }

        return await ComposeAndSaveAsync(
            resolved, request.RequestedActivityType, request.Title ?? lesson.Title,
            lesson.CefrLevel, lesson.Skill, lesson.Subskill,
            ParseTags(lesson.ContextTagsJson), ParseTags(lesson.FocusTagsJson), lesson.DifficultyBand,
            request.Notes, request.CreatedByUserId, lessonId: lesson.Id, ct);
    }

    /// <summary>Phase K19 — the only Exercise composer sourced from a Lesson's own Body/Title
    /// instead of a Resource Bank row; see <see cref="ActivityTypeLessonReflection"/>'s doc
    /// comment. Deterministic, open-ended, honestly marked
    /// <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> — reflection has no
    /// "correct answer" by nature. Creates zero <see cref="ExerciseResourceLink"/> rows (there is
    /// no resource this Exercise is derived from).</summary>
    private async Task<GenerateExerciseResult> ComposeAndSaveLessonReflectionAsync(
        Lesson lesson, string? title, string? notes, Guid? createdByUserId, CancellationToken ct)
    {
        // No "is Body empty" guard here — Lesson's own constructor already guarantees a
        // non-whitespace Body (ValidateAuthorableFields), so that state can't occur.
        var lessonBody = lesson.Body.Trim();
        var instructions = "Reflect on what you just learned in this Lesson.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "lesson", html = $"<p>{System.Net.WebUtility.HtmlEncode(lessonBody)}</p>" },
                new { type = "textarea", key = "answer", label = "What was the key takeaway for you, and how will you use it?", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Reflection — not deterministically scored, no manual/AI evaluation required either.",
        });

        var schemaCheck = _schemaValidator.ValidateSchema(formSchemaJson);
        if (!schemaCheck.IsValid)
            throw new ExerciseValidationException($"Generated Form.io schema failed validation: {schemaCheck.Error}");

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : $"{lesson.Title} — Reflection";
        var description = $"Deterministic draft — review and edit before approval. Generated from Lesson: {lesson.Title}."
            + (notes is not null ? $" {notes.Trim()}" : string.Empty);

        Exercise activity;
        try
        {
            activity = new Exercise(
                resolvedTitle, instructions, ActivityTypeLessonReflection, ExerciseRendererType.Formio, ExerciseSourceMode.GeneratedFromLesson,
                description, patternKey: null, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson,
                lesson.CefrLevel, "Reflection", subskill: null,
                contextTagsJson: "[]", focusTagsJson: "[]",
                difficultyBand: lesson.DifficultyBand, estimatedMinutes: null, lesson.Id,
                GenerationProvider, GenerationModel, createdByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException(ex.Message);
        }

        _db.Exercises.Add(activity);
        await _db.SaveChangesAsync(ct);

        var dto = ExerciseMappers.ToDto(activity, Array.Empty<ExerciseResourceLink>());
        return new GenerateExerciseResult(dto, $"/admin/exercises?id={activity.Id}");
    }

    private async Task<GenerateExerciseResult> ComposeAndSaveAsync(
        List<ResolvedResource> resolved,
        string? requestedActivityType,
        string? title,
        string? defaultCefrLevel,
        string? defaultSkill,
        string? defaultSubskill,
        IReadOnlyList<string>? defaultContextTags,
        IReadOnlyList<string>? defaultFocusTags,
        int? defaultDifficultyBand,
        string? notes,
        Guid? createdByUserId,
        Guid lessonId,
        CancellationToken ct)
    {
        var primaryMatch = resolved.FirstOrDefault(r => r.Role == LessonResourceRole.Primary);
        var primary = primaryMatch.Snapshot is not null ? primaryMatch : resolved[0];

        // Phase K17 — was a binary isDefinitional (Vocab/Grammar) vs everything-else split; now
        // resource-type-driven since Writing resources need their own supported-type bucket
        // distinct from Reading's.
        var supportedForCategory = primary.Type switch
        {
            PublishedResourceType.Vocabulary or PublishedResourceType.Grammar =>
                new[] { ActivityTypeGapFill, ActivityTypeMultipleChoiceSingle, ActivityTypePhraseMatch },
            PublishedResourceType.ReadingReference or PublishedResourceType.ReadingPassage =>
                new[]
                {
                    ActivityTypeShortAnswer, ActivityTypeReadingFillInBlanks, ActivityTypeReadingWritingFillInBlanks,
                    ActivityTypeSummarizeWrittenText, ActivityTypeReorderParagraphs,
                },
            PublishedResourceType.Writing =>
                new[] { ActivityTypeEmailReply, ActivityTypeOpenWritingTask, ActivityTypeWriteEssay, ActivityTypeTeamsChatSimulation },
            PublishedResourceType.Listening =>
                new[]
                {
                    ActivityTypeListeningFillInBlanks,
                    ActivityTypeSummarizeSpokenText,
                    ActivityTypeRetellLecture,
                    ActivityTypeSummarizeGroupDiscussion,
                    ActivityTypeHighlightIncorrectWords,
                    ActivityTypeWriteFromDictation,
                },
            PublishedResourceType.Speaking =>
                new[]
                {
                    ActivityTypeSpokenResponseFromPrompt,
                    ActivityTypeRespondToSituation,
                    ActivityTypeAnswerShortQuestion,
                    ActivityTypeSpeakingRoleplayTurn,
                    ActivityTypeReadAloud,
                    ActivityTypeDescribeImage,
                    ActivityTypeRepeatSentence,
                },
            _ => Array.Empty<string>(),
        };
        if (supportedForCategory.Length == 0)
            throw new ExerciseValidationException(
                $"No Exercise types are supported for resource type '{primary.Type}' yet.");

        var activityType = requestedActivityType?.Trim().ToLowerInvariant() ?? supportedForCategory[0];
        if (!supportedForCategory.Contains(activityType))
            throw new ExerciseValidationException(
                $"Activity type '{activityType}' is not supported for resource type '{primary.Type}'. Supported: {string.Join(", ", supportedForCategory)}.");

        var (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson) = activityType switch
        {
            ActivityTypeGapFill => ComposeGapFill(primary.Snapshot),
            ActivityTypeMultipleChoiceSingle => await ComposeMultipleChoiceSingleAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypePhraseMatch => await ComposePhraseMatchAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeShortAnswer => ComposeShortAnswer(primary.Snapshot),
            ActivityTypeReadingFillInBlanks => await ComposeReadingFillInBlanksAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeReadingWritingFillInBlanks => await ComposeReadingWritingFillInBlanksAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeSummarizeWrittenText => ComposeSummarizeWrittenText(primary.Snapshot),
            ActivityTypeReorderParagraphs => await ComposeReorderParagraphsAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeEmailReply => ComposeWritingPrompt(primary.Snapshot,
                "Read the scenario below and write your email reply.", "Your email reply"),
            ActivityTypeOpenWritingTask => ComposeWritingPrompt(primary.Snapshot,
                "Complete the writing task below.", "Your response"),
            ActivityTypeWriteEssay => ComposeWritingPrompt(primary.Snapshot,
                "Read the essay prompt below and write a structured response.", "Your essay"),
            ActivityTypeTeamsChatSimulation => ComposeWritingPrompt(primary.Snapshot,
                "Read the chat message below and write a concise, professional reply.", "Your reply"),
            ActivityTypeListeningFillInBlanks => ComposeListeningFillInBlanks(primary.Snapshot),
            ActivityTypeHighlightIncorrectWords => ComposeHighlightIncorrectWords(primary.Snapshot),
            ActivityTypeWriteFromDictation => ComposeWriteFromDictation(primary.Snapshot),
            ActivityTypeRepeatSentence => ComposeRepeatSentence(primary.Snapshot),
            ActivityTypeSpokenResponseFromPrompt => ComposeSpeakingPrompt(primary.Snapshot,
                "Respond aloud to the prompt below."),
            ActivityTypeRespondToSituation => ComposeSpeakingPrompt(primary.Snapshot,
                "Read the situation below and speak an appropriate response."),
            ActivityTypeAnswerShortQuestion => ComposeSpeakingPrompt(primary.Snapshot,
                "Read the question below and speak your answer clearly."),
            ActivityTypeSpeakingRoleplayTurn => ComposeSpeakingPrompt(primary.Snapshot,
                "Read the roleplay scenario below and record your spoken turn."),
            ActivityTypeReadAloud => ComposeSpeakingPrompt(primary.Snapshot,
                "Read the text below aloud, as clearly and naturally as possible."),
            ActivityTypeDescribeImage => await ComposeDescribeImageAsync(primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeSummarizeSpokenText => ComposeWritingPrompt(primary.Snapshot,
                "Read the transcript below and write a concise summary in your own words.", "Your summary"),
            ActivityTypeRetellLecture => ComposeSpeakingPrompt(primary.Snapshot,
                "Read the lecture transcript below and retell the main ideas in your own words."),
            ActivityTypeSummarizeGroupDiscussion => ComposeSpeakingPrompt(primary.Snapshot,
                "Read the discussion transcript below and summarize the main points, speaker views, and outcomes."),
            _ => throw new ExerciseValidationException($"Unsupported activity type '{activityType}'."),
        };

        var schemaCheck = _schemaValidator.ValidateSchema(formSchemaJson);
        if (!schemaCheck.IsValid)
            throw new ExerciseValidationException($"Generated Form.io schema failed validation: {schemaCheck.Error}");

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : primary.Snapshot.Title;
        var cefrLevel = defaultCefrLevel ?? primary.Snapshot.CefrLevel;
        var skill = defaultSkill ?? primary.Snapshot.Skill;
        var subskill = defaultSubskill ?? primary.Snapshot.Subskill;
        var contextTags = defaultContextTags is { Count: > 0 }
            ? defaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = defaultFocusTags is { Count: > 0 }
            ? defaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = defaultDifficultyBand ?? primary.Snapshot.DifficultyBand;

        var description = $"Deterministic draft — review and edit before approval. Generated from: "
            + string.Join(", ", resolved.Select(r => r.Snapshot.Title)) + "."
            + (notes is not null ? $" {notes.Trim()}" : string.Empty);

        Exercise activity;
        try
        {
            activity = new Exercise(
                resolvedTitle, instructions, activityType, ExerciseRendererType.Formio, ExerciseSourceMode.GeneratedFromLesson,
                description, patternKey: null, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes: null, lessonId,
                GenerationProvider, GenerationModel, createdByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException(ex.Message);
        }

        _db.Exercises.Add(activity);
        await _db.SaveChangesAsync(ct);

        var links = resolved
            .Select(r => new ExerciseResourceLink(
                activity.Id, r.Type, r.Input.ResourceId, r.Role, r.Snapshot.Title, r.Snapshot.ContentFingerprint))
            .ToList();
        _db.ExerciseResourceLinks.AddRange(links);
        await _db.SaveChangesAsync(ct);

        var dto = ExerciseMappers.ToDto(activity, links);
        return new GenerateExerciseResult(dto, $"/admin/exercises?id={activity.Id}");
    }

    /// <summary>Shows the resource's own definition/description, asks the student to type the
    /// term back — needs no distractor pool, so it's the safe default for any Vocabulary/Grammar
    /// resource regardless of how much other bank content exists.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeGapFill(LessonResourceSnapshot primary)
    {
        var term = primary.Title;
        var definition = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no definition available)";

        var instructions = "Read the definition below and type the missing word or phrase.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(definition)}</p>" },
                new { type = "textfield", key = "answer", label = "Your answer", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = term });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, CorrectAnswer: term, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the answer was \"{term}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Asks "what does X mean" with distractor options pulled from sibling published
    /// resources of the same type. Rejects (throws) rather than degrading to a single-option
    /// "choice" when no usable distractor exists.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeMultipleChoiceSingleAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid primaryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(primary.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no definition/description text to build a multiple-choice question from — use 'gap_fill' instead.");

        var distractors = await FindDistractorDefinitionsAsync(type, primaryId, primary.CefrLevel, ct);
        if (distractors.Count == 0)
            throw new ExerciseValidationException(
                "No other published resources of the same type have a definition/description to use as distractors — use 'gap_fill' instead.");

        var options = new List<(string Key, string Text)> { ("opt_0", primary.Body!.Trim()) };
        for (var i = 0; i < distractors.Count; i++)
            options.Add(($"opt_{i + 1}", distractors[i]));

        var instructions = $"Choose the correct meaning of \"{primary.Title}\".";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new
                {
                    type = "radio", key = "answer", label = instructions, input = true,
                    values = options.Select(o => new { label = o.Text, value = o.Key }).ToArray(),
                },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = options[0].Text });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.SingleChoice, CorrectAnswer: options[0].Key, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the correct meaning was: \"{options[0].Text}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K16 — decomposes "matching" into N independent single_choice
    /// sub-questions, one radio per term, each offering every pulled term's own definition as an
    /// option — see <see cref="ActivityTypePhraseMatch"/>'s doc comment for the full rationale.
    /// Needs at least <see cref="MinPhraseMatchPairs"/> total terms (primary + siblings) with a
    /// usable title+definition; rejects rather than degrading to a thin 1-2-term exercise.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposePhraseMatchAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid primaryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(primary.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no definition/description text to build a phrase-match exercise from — use 'gap_fill' instead.");

        var siblings = await FindSiblingTermDefinitionsAsync(type, primaryId, primary.CefrLevel, ct);
        var pairs = new List<(string Title, string Body)> { (primary.Title, primary.Body!.Trim()) };
        pairs.AddRange(siblings);

        if (pairs.Count < MinPhraseMatchPairs)
            throw new ExerciseValidationException(
                $"Not enough published {type} resources with a definition/description to build a phrase-match exercise (need at least {MinPhraseMatchPairs}, found {pairs.Count}) — use 'gap_fill' instead.");

        var options = pairs.Select((p, i) => (Key: $"opt_{i}", Text: p.Body)).ToList();
        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();
        var components = new List<object>();

        for (var i = 0; i < pairs.Count; i++)
        {
            var key = $"answer_{i}";
            answerKey[key] = pairs[i].Body;
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.SingleChoice, CorrectAnswer: options[i].Key, Points: 1.0);
            components.Add(new
            {
                type = "radio", key, label = $"What does \"{pairs[i].Title}\" mean?", input = true,
                values = options.Select(o => new { label = o.Text, value = o.Key }).ToArray(),
            });
        }

        var instructions = "Match each term below to its correct meaning.";
        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = "Some matches were incorrect — review the terms and their meanings.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Open-ended comprehension prompt, honestly marked as requiring manual/AI
    /// evaluation — reading comprehension can't be deterministically graded from a short
    /// excerpt/summary.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeShortAnswer(LessonResourceSnapshot primary)
    {
        var excerpt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no excerpt available)";

        var instructions = "Read the passage below and answer the question in one or two sentences.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(excerpt)}</p>" },
                new { type = "textarea", key = "answer", label = $"What is \"{primary.Title}\" mainly about?", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K17 — Writing-skill but Reading-resource-sourced. Same shape as
    /// ComposeShortAnswer (excerpt shown, open-ended textarea, honestly unscored), different
    /// framing: asks for a summary rather than an answer to a specific comprehension
    /// question.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeSummarizeWrittenText(LessonResourceSnapshot primary)
    {
        var excerpt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no excerpt available)";

        var instructions = "Read the passage below and write a concise summary in your own words.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(excerpt)}</p>" },
                new { type = "textarea", key = "answer", label = "Your summary", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K17 — email_reply/open_writing_task/write_essay. Deterministic, like
    /// short_answer: the prompt shown is always the Writing resource's own PromptText verbatim,
    /// honestly marked as requiring manual/AI evaluation. There is no correctness question here
    /// (nothing is scored), so — unlike the reading comprehension MC types — there is no reason to
    /// route this through AI at all.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeWritingPrompt(LessonResourceSnapshot primary, string instructions, string responseLabel)
    {
        var prompt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no prompt text available)";

        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(prompt)}</p>" },
                new { type = "textarea", key = "answer", label = responseLabel, input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K18 — spoken_response_from_prompt/respond_to_situation/answer_short_question/
    /// speaking_roleplay_turn/read_aloud. Deterministic, Speaking resources only: shows the
    /// resource's own PromptText verbatim, student responds via the stock "speakingResponse"
    /// Form.io component (same one already used by placement/onboarding speaking items).
    /// Honestly marked <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> —
    /// <see cref="ScoringRuleKinds.Speaking"/> scoring (<c>IPlacementSpeakingScorer</c>) is
    /// currently hardwired to the Placement flow only, not reusable by the generic
    /// ComponentAnswerScorer path Exercise attempts actually go through — wiring real audio
    /// scoring into the bank-first pipeline is a separately-scoped follow-up, not done here.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeSpeakingPrompt(LessonResourceSnapshot primary, string instructions)
    {
        var prompt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no prompt text available)";

        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(prompt)}</p>" },
                new { type = "speakingResponse", key = "answer", label = "Your spoken response" },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.Speaking, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended spoken response — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K20 — same speakingResponse/unscored shape as
    /// <see cref="ComposeSpeakingPrompt"/>, but shows an <c>&lt;img&gt;</c> instead of text —
    /// see <see cref="ActivityTypeDescribeImage"/>'s doc comment for why generation is rejected
    /// when the Speaking resource has no ImageUrl.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeDescribeImageAsync(LessonResourceSnapshot primary, Guid resourceId, CancellationToken ct)
    {
        var imageUrl = await FindSpeakingImageUrlAsync(resourceId, ct);
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no image set — add one on the Resource Bank edit page, or use a different Exercise type.");

        var instructions = "Look at the image below and describe what you see, as clearly and naturally as possible.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "image", html = $"<img src=\"{System.Net.WebUtility.HtmlEncode(imageUrl)}\" alt=\"Describe this image\" style=\"max-width:100%;\" />" },
                new { type = "speakingResponse", key = "answer", label = "Your spoken description" },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.Speaking, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended spoken response — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    private async Task<string?> FindSpeakingImageUrlAsync(Guid resourceId, CancellationToken ct)
    {
        var json = await _db.ResourceBankItems
            .Where(e => e.Id == resourceId && e.Type == PublishedResourceType.Speaking)
            .Select(e => e.ContentJson)
            .FirstOrDefaultAsync(ct);
        return json is null ? null : ResourceBankItemContent.Deserialize<SpeakingPromptContent>(json).ImageUrl;
    }

    /// <summary>Phase K16 — deterministic cloze over the resource's own excerpt/passage text.
    /// ReadingPassage's <see cref="LessonResourceSnapshot.Body"/> prefers Summary over PassageText
    /// (see <see cref="LessonResourceLookup.FindAsync"/>), which is too short for a meaningful
    /// cloze — this re-fetches the full PassageText directly for that type. ReadingReference has
    /// no separate summary field, so its Snapshot.Body (ReferenceExcerpt) is used as-is.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeReadingFillInBlanksAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid resourceId, CancellationToken ct)
    {
        var sourceText = type == PublishedResourceType.ReadingPassage
            ? await FindFullPassageTextAsync(resourceId, ct) ?? primary.Body
            : primary.Body;

        return BuildCloze(sourceText, primary.Title,
            "Read the passage below and fill in each numbered blank.", "use 'short_answer' instead");
    }

    /// <summary>Phase K17 — same deterministic cloze algorithm as
    /// <see cref="ComposeReadingFillInBlanksAsync"/>, sourced from a Listening resource's own
    /// transcript instead of a Reading excerpt/passage. Unlike ReadingPassage, Listening has no
    /// Summary-vs-full-text divergence — <see cref="LessonResourceSnapshot.Body"/> already carries
    /// the transcript verbatim (see <see cref="LessonResourceLookup.FindAsync"/>) — so no
    /// re-fetch is needed. A Listening resource published without a transcript (audio-only) is
    /// valid data, so this rejects rather than degrading, same discipline as everywhere else.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeListeningFillInBlanks(LessonResourceSnapshot primary) =>
        BuildCloze(primary.Body, primary.Title,
            "Read the transcript below and fill in each numbered blank.", "publish a transcript, or use a different Exercise type");

    /// <summary>Phase K21 — see <see cref="ActivityTypeHighlightIncorrectWords"/>'s doc comment
    /// for the full rationale. Picks up to <see cref="MaxHighlightAlteredWords"/> distinct
    /// eligible content words (same length&gt;=5/alphabetic/distinct filter as BuildCloze), then
    /// rotates their text among each other's positions — word[i] displays word[i+1]'s text (wrapping
    /// around) — so every altered word is a real word actually said elsewhere in the transcript,
    /// and (since all rotated words are distinct by construction) always differs from its own
    /// original slot. The audioPlayer component carries no source in its own schema; the student's
    /// browser resolves it via the resource-audio streaming endpoint (see
    /// ExerciseRendererComponent.formIoResourceAudioUrl / ActivityController.GetResourceAudio).</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeHighlightIncorrectWords(LessonResourceSnapshot primary)
    {
        var transcript = primary.Body;
        if (string.IsNullOrWhiteSpace(transcript))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no transcript to build a highlight-incorrect-words exercise from — publish a transcript, or use a different Exercise type.");

        var words = transcript.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var alteredIndexes = new List<int>();
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < words.Length && alteredIndexes.Count < MaxHighlightAlteredWords; i++)
        {
            var clean = words[i].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            if (clean.Length < MinClozeWordLength || !clean.All(char.IsLetter)) continue;
            if (!seenWords.Add(clean)) continue;
            alteredIndexes.Add(i);
        }

        if (alteredIndexes.Count < MinHighlightAlteredWords)
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' does not have enough distinct content words to build a highlight-incorrect-words exercise (need at least {MinHighlightAlteredWords}, found {alteredIndexes.Count}) — use a different Exercise type.");

        var originalCleanWords = alteredIndexes
            .Select(idx => words[idx].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')'))
            .ToList();

        var tokens = new List<(string Id, string Text)>();
        var alteredTokenIds = new List<string>();
        var alteredSet = new HashSet<int>(alteredIndexes);
        for (var i = 0; i < words.Length; i++)
        {
            var id = $"t{i}";
            var rotatedPosition = alteredIndexes.IndexOf(i);
            var text = rotatedPosition >= 0
                ? originalCleanWords[(rotatedPosition + 1) % originalCleanWords.Count]
                : words[i];
            tokens.Add((id, text));
            if (alteredSet.Contains(i)) alteredTokenIds.Add(id);
        }

        var instructions = "Listen to the audio, then select every displayed word that does not match what you heard.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "audioPlayer", key = "listening_audio" },
                new
                {
                    type = "highlightWords", key = "answer", input = true,
                    label = instructions,
                    tokens = tokens.Select(t => new { id = t.Id, text = t.Text }).ToArray(),
                },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string[]> { ["answer"] = alteredTokenIds.ToArray() });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.MultipleChoice, CorrectAnswers: alteredTokenIds, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"The audio actually said: {string.Join(", ", originalCleanWords)}.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Splits free text into trimmed sentences on '.', '!', '?', discarding anything
    /// shorter than <see cref="MinDictationSentenceLength"/> characters (too short to be a
    /// meaningful dictation/repeat unit — e.g. a stray "Mr." abbreviation fragment). Shared by
    /// <see cref="ComposeWriteFromDictation"/> and <see cref="ComposeRepeatSentence"/>.</summary>
    private static List<string> SplitIntoSentences(string text) =>
        text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length >= MinDictationSentenceLength)
            .ToList();

    /// <summary>Phase K22 — see <see cref="ActivityTypeWriteFromDictation"/>'s doc comment for the
    /// full rationale. The audioPlayer component carries no source in its own schema; the
    /// student's browser resolves the resource's own full audio via the Phase K21 resource-audio
    /// streaming endpoint (same mechanism as highlight_incorrect_words).</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeWriteFromDictation(LessonResourceSnapshot primary)
    {
        var transcript = primary.Body;
        if (string.IsNullOrWhiteSpace(transcript))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no transcript to build a dictation exercise from — publish a transcript, or use a different Exercise type.");

        var sentences = SplitIntoSentences(transcript).Take(MaxDictationSentences).ToList();
        if (sentences.Count < MinDictationSentences)
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' does not have enough distinct sentences to build a dictation exercise (need at least {MinDictationSentences}, found {sentences.Count}) — use a different Exercise type.");

        var instructions = "Listen to the audio, then type exactly what you hear for each sentence below.";
        var components = new List<object> { new { type = "audioPlayer", key = "listening_audio" } };
        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();
        for (var i = 0; i < sentences.Count; i++)
        {
            var key = $"answer_{i}";
            answerKey[key] = sentences[i];
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.TextNormalized, CorrectAnswer: sentences[i], Points: 1.0);
            components.Add(new { type = "textfield", key, label = $"Sentence {i + 1}", input = true });
        }

        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"The audio actually said: {string.Join(" / ", sentences)}.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K22 — see <see cref="ActivityTypeRepeatSentence"/>'s doc comment for the
    /// full rationale.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeRepeatSentence(LessonResourceSnapshot primary)
    {
        if (string.IsNullOrWhiteSpace(primary.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no prompt text to build a repeat-sentence exercise from — use a different Exercise type.");

        var sentences = SplitIntoSentences(primary.Body).Take(MaxRepeatSentences).ToList();
        if (sentences.Count < MinRepeatSentences)
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' does not have enough distinct sentences to build a repeat-sentence exercise (need at least {MinRepeatSentences}, found {sentences.Count}) — use a different Exercise type.");

        var instructions = "Read each sentence below, then repeat it aloud as accurately as you can.";
        var components = new List<object>();
        for (var i = 0; i < sentences.Count; i++)
            components.Add(new { type = "speakingResponse", key = $"answer_{i}", label = $"Repeat: \"{sentences[i]}\"" });

        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(sentences.Select((s, i) => new { key = $"answer_{i}", text = s })
            .ToDictionary(x => x.key, x => (string?)x.text));
        var scoringComponents = sentences.Select((_, i) => $"answer_{i}")
            .ToDictionary(key => key, _ => new ComponentScoringRule(ScoringRuleKinds.Speaking, RequiresManualOrAiEvaluation: true));
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended spoken response — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K18 — "choose the correct word for each blank", not "type the word":
    /// otherwise identical word-selection to <see cref="ComposeReadingFillInBlanksAsync"/>, but
    /// each blank renders as a radio choice (correct word + 2 distractors drawn from the same
    /// text's other content words) instead of a free-text field, scored per-blank via
    /// <see cref="ScoringRuleKinds.SingleChoice"/> — same "never AI-supplied correct answer"
    /// shape, both correct answer and distractors come straight from the resource's own text.
    /// Needs at least 3 distinct content words (1 to blank + 2 more to use as distractors) —
    /// rejects rather than degrades to fewer than 2 distractor options.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeReadingWritingFillInBlanksAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid resourceId, CancellationToken ct)
    {
        var sourceText = type == PublishedResourceType.ReadingPassage
            ? await FindFullPassageTextAsync(resourceId, ct) ?? primary.Body
            : primary.Body;

        if (string.IsNullOrWhiteSpace(sourceText))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no source text to build word-choice blanks from — use 'short_answer' instead.");

        var words = sourceText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var candidateIndexes = new List<int>();
        var candidateWords = new List<string>();
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < words.Length; i++)
        {
            var clean = words[i].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            if (clean.Length < MinClozeWordLength || !clean.All(char.IsLetter)) continue;
            if (!seenWords.Add(clean)) continue;
            candidateIndexes.Add(i);
            candidateWords.Add(clean);
        }

        if (candidateWords.Count < 3)
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' does not have enough distinct content words to build word-choice blanks (need at least 3) — use 'reading_fill_in_blanks' instead.");

        var blankCount = Math.Min(MaxClozeBlanks, candidateWords.Count);
        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();
        var components = new List<object>();
        var displayWords = new List<string>(words);

        for (var b = 0; b < blankCount; b++)
        {
            var idx = candidateIndexes[b];
            var correctWord = candidateWords[b];
            displayWords[idx] = $"({b + 1}) _____";

            var distractors = new List<string>();
            for (var offset = 1; distractors.Count < 2 && offset < candidateWords.Count; offset++)
            {
                var candidate = candidateWords[(b + offset) % candidateWords.Count];
                if (!string.Equals(candidate, correctWord, StringComparison.OrdinalIgnoreCase))
                    distractors.Add(candidate);
            }

            var options = new List<(string Key, string Text)> { ("opt_0", correctWord) };
            for (var d = 0; d < distractors.Count; d++)
                options.Add(($"opt_{d + 1}", distractors[d]));

            var key = $"answer_{b}";
            answerKey[key] = correctWord;
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.SingleChoice, CorrectAnswer: "opt_0", Points: 1.0);
            components.Add(new
            {
                type = "radio", key, label = $"Blank {b + 1}", input = true,
                values = options.Select(o => new { label = o.Text, value = o.Key }).ToArray(),
            });
        }

        var clozeHtml = System.Net.WebUtility.HtmlEncode(string.Join(' ', displayWords));
        components.Insert(0, new { type = "content", key = "passage", html = $"<p>{clozeHtml}</p>" });

        var instructions = "Read the passage below and choose the correct word for each numbered blank.";
        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"The correct words were: {string.Join(", ", answerKey.Values)}.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K16 — see <see cref="ActivityTypeReorderParagraphs"/>'s doc comment for the
    /// full rationale (stock Form.io datagrid+reorder pattern, proven-working shape, scored via
    /// <see cref="ScoringRuleKinds.OrderedSequence"/>). Only meaningful for ReadingPassage — a
    /// ReadingReference excerpt is one short paragraph, not multi-paragraph text; splitting
    /// naturally rejects it via the paragraph-count check below rather than needing a separate
    /// resource-type gate. Row order is deterministically shuffled (fixed-seed <see cref="Random"/>)
    /// rather than left in original order, so the exercise isn't trivially "already correct".</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeReorderParagraphsAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid resourceId, CancellationToken ct)
    {
        var sourceText = type == PublishedResourceType.ReadingPassage
            ? await FindFullPassageTextAsync(resourceId, ct) ?? primary.Body
            : primary.Body;

        if (string.IsNullOrWhiteSpace(sourceText))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no passage text to build a reorder exercise from — use 'short_answer' instead.");

        var paragraphs = sourceText
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (paragraphs.Count < MinReorderParagraphs)
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' does not have enough distinct paragraphs to build a reorder exercise (need at least {MinReorderParagraphs}, found {paragraphs.Count}) — use 'short_answer' instead.");

        if (paragraphs.Count > MaxReorderParagraphs)
            paragraphs = paragraphs.Take(MaxReorderParagraphs).ToList();

        var correctOrder = Enumerable.Range(0, paragraphs.Count).Select(i => $"p{i}").ToList();

        var shuffled = Enumerable.Range(0, paragraphs.Count).ToList();
        var rng = new Random(paragraphs.Count); // fixed seed — deterministic, reproducible, not a true shuffle each call
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var rows = shuffled.Select(i => new { itemId = $"p{i}", text = paragraphs[i] }).ToArray();

        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new
                {
                    type = "datagrid", key = "paragraphs", reorder = true, disableAddingRemovingRows = true,
                    defaultValue = rows,
                    components = new object[]
                    {
                        new { type = "hidden", key = "itemId" },
                        new { type = "textarea", key = "text", disabled = true },
                    },
                },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, List<string>> { ["paragraphs"] = correctOrder });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["paragraphs"] = new(ScoringRuleKinds.OrderedSequence, CorrectOrder: correctOrder, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct order!",
            incorrectFeedback = "Not quite the right order — review the passage and try again.",
        });

        var instructions = "Drag the paragraphs below into the correct logical order.";
        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Shared deterministic cloze-building core: blanks out up to
    /// <see cref="MaxClozeBlanks"/> distinct content words (length &gt;= <see cref="MinClozeWordLength"/>,
    /// alphabetic) from <paramref name="sourceText"/>, text_normalized scoring per blank. Rejects
    /// rather than degrades when fewer than 2 usable content words exist.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        BuildCloze(string? sourceText, string resourceTitle, string instructions, string rejectionHint)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
            throw new ExerciseValidationException(
                $"Resource '{resourceTitle}' has no source text to build a cloze from — {rejectionHint}.");

        var words = sourceText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var blankIndexes = new List<int>();
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < words.Length && blankIndexes.Count < MaxClozeBlanks; i++)
        {
            var clean = words[i].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            if (clean.Length < MinClozeWordLength || !clean.All(char.IsLetter)) continue;
            if (!seenWords.Add(clean)) continue;
            blankIndexes.Add(i);
        }

        if (blankIndexes.Count < 2)
            throw new ExerciseValidationException(
                $"Resource '{resourceTitle}' does not have enough distinct content words to build a cloze — {rejectionHint}.");

        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();
        var displayWords = new List<string>(words);
        for (var b = 0; b < blankIndexes.Count; b++)
        {
            var idx = blankIndexes[b];
            var clean = words[idx].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            var key = $"answer_{b}";
            answerKey[key] = clean;
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.TextNormalized, CorrectAnswer: clean, Points: 1.0);
            displayWords[idx] = $"({b + 1}) _____";
        }

        var clozeHtml = System.Net.WebUtility.HtmlEncode(string.Join(' ', displayWords));
        var components = new List<object>
        {
            new { type = "content", key = "passage", html = $"<p>{clozeHtml}</p>" },
        };
        for (var b = 0; b < blankIndexes.Count; b++)
            components.Add(new { type = "textfield", key = $"answer_{b}", label = $"Blank {b + 1}", input = true });

        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"The correct words were: {string.Join(", ", answerKey.Values)}.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Picks the first eligible content word (length &gt;= <see cref="MinClozeWordLength"/>,
    /// alphabetic) from <paramref name="sourceText"/> and returns it alongside the source text with
    /// that one occurrence replaced by a blank marker. Public/static so
    /// <see cref="AiExerciseGenerationService"/> can reuse it for select_missing_word — the correct
    /// answer there is this deterministically-picked word, never AI-supplied; AI is only asked for
    /// wrong-word distractors, same safe shape as multiple_choice_single.</summary>
    public static (string Word, string DisplayTextWithBlank)? PickBlankWord(string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return null;

        var words = sourceText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var clean = words[i].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            if (clean.Length < MinClozeWordLength || !clean.All(char.IsLetter)) continue;

            var displayWords = new List<string>(words) { [i] = "_____" };
            return (clean, string.Join(' ', displayWords));
        }
        return null;
    }

    private async Task<string?> FindFullPassageTextAsync(Guid resourceId, CancellationToken ct)
    {
        var json = await _db.ResourceBankItems
            .Where(e => e.Id == resourceId && e.Type == PublishedResourceType.ReadingPassage)
            .Select(e => e.ContentJson)
            .FirstOrDefaultAsync(ct);
        return json is null ? null : ResourceBankItemContent.Deserialize<ReadingPassageContent>(json).PassageText;
    }

    private async Task<List<string>> FindDistractorDefinitionsAsync(
        PublishedResourceType type, Guid excludeId, string? cefrLevel, CancellationToken ct)
    {
        if (type is not (PublishedResourceType.Vocabulary or PublishedResourceType.Grammar))
            return new List<string>();

        var rows = await _db.ResourceBankItems
            .Where(e => e.Type == type && e.Id != excludeId)
            .OrderByDescending(e => e.CefrLevel == cefrLevel)
            .ThenBy(e => e.CreatedAt)
            .Take(MaxDistractors * 2) // over-fetch — some rows may lack a usable definition/description
            .Select(e => e.ContentJson)
            .ToListAsync(ct);

        var bodies = type == PublishedResourceType.Vocabulary
            ? rows.Select(json => ResourceBankItemContent.Deserialize<VocabularyContent>(json).Notes)
            : rows.Select(json => ResourceBankItemContent.Deserialize<GrammarContent>(json).Description);

        return bodies
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b!.Trim())
            .Distinct()
            .Take(MaxDistractors)
            .ToList();
    }

    /// <summary>Same sibling-lookup pattern as <see cref="FindDistractorDefinitionsAsync"/>
    /// (same CEFR level preferred, newest-first tiebreak), but keeps each sibling's title too —
    /// phrase_match needs both, unlike multiple_choice_single's distractors which only need
    /// text.</summary>
    private async Task<List<(string Title, string Body)>> FindSiblingTermDefinitionsAsync(
        PublishedResourceType type, Guid excludeId, string? cefrLevel, CancellationToken ct)
    {
        if (type is not (PublishedResourceType.Vocabulary or PublishedResourceType.Grammar))
            return new List<(string, string)>();

        var rows = await _db.ResourceBankItems
            .Where(e => e.Type == type && e.Id != excludeId)
            .OrderByDescending(e => e.CefrLevel == cefrLevel)
            .ThenBy(e => e.CreatedAt)
            .Take(MaxPhraseMatchSiblings * 2) // over-fetch — some rows may lack a usable definition/description
            .Select(e => e.ContentJson)
            .ToListAsync(ct);

        var pairs = new List<(string Title, string Body)>();
        foreach (var json in rows)
        {
            string? title;
            string? body;
            if (type == PublishedResourceType.Vocabulary)
            {
                var c = ResourceBankItemContent.Deserialize<VocabularyContent>(json);
                title = c.Word;
                body = c.Notes;
            }
            else
            {
                var c = ResourceBankItemContent.Deserialize<GrammarContent>(json);
                title = c.GrammarPoint;
                body = c.Description;
            }

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(body))
                pairs.Add((title!, body!.Trim()));
            if (pairs.Count >= MaxPhraseMatchSiblings) break;
        }
        return pairs;
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static List<string> MergeTagArrays(IEnumerable<string?> jsonArrays)
    {
        var merged = new List<string>();
        foreach (var json in jsonArrays)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(json);
                if (tags is null) continue;
                foreach (var tag in tags)
                    if (!string.IsNullOrWhiteSpace(tag) && !merged.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        merged.Add(tag);
            }
            catch (JsonException)
            {
                // Malformed tag JSON on a source row is never fatal to generation — skip it.
            }
        }
        return merged;
    }
}
