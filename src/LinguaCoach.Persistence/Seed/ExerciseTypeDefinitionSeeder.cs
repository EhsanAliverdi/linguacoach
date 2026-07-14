using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

public static class ExerciseTypeDefinitionSeeder
{
    // Per-key practice/option counts: key => (minItems, defaultItems, maxItems, minOptions, defaultOptions, maxOptions).
    // Counts are configuration only. They never affect readiness or runnable status.
    // WorkloadModeRegistry in ModuleStageContentValidator classifies each pattern as
    // SingleSubstantialTask (one item is the full exercise) or MultiItem (multiple items expected).
    // For MultiItem formats, MinItems >= 2 triggers workload sanity enforcement in the validator.
    private static readonly Dictionary<string, (int MinItems, int DefItems, int MaxItems, int MinOpts, int DefOpts, int MaxOpts)> CountOverrides = new(StringComparer.Ordinal)
    {
        // Pattern-backed multi-item formats (MinItems >= 2 → workload sanity enforced)
        ["phrase_match"] = (2, 5, 8, 0, 0, 0),
        ["gap_fill_workplace_phrase"] = (2, 5, 8, 0, 0, 0),
        ["listen_and_gap_fill"] = (2, 4, 6, 0, 0, 0),
        ["listen_and_answer"] = (2, 3, 5, 0, 0, 0),
        // Reading (multi-item fill/reorder)
        ["reading_multiple_choice_single"] = (1, 1, 1, 3, 4, 5),
        ["reading_multiple_choice_multi"] = (1, 1, 1, 4, 4, 6),
        ["reading_fill_in_blanks"] = (3, 4, 6, 3, 4, 5),
        ["reading_writing_fill_in_blanks"] = (3, 4, 6, 3, 4, 5),
        ["reorder_paragraphs"] = (4, 4, 5, 0, 0, 0),
        // Writing — single-substantial-task (MinItems = 1 is correct, exempt from workload enforcement)
        ["summarize_written_text"] = (1, 1, 1, 0, 0, 0),
        ["write_essay"] = (1, 1, 1, 0, 0, 0),
        // Listening (single-task formats)
        ["listening_multiple_choice_single"] = (1, 1, 1, 3, 4, 5),
        ["listening_multiple_choice_multi"] = (1, 1, 1, 4, 4, 6),
        ["listening_fill_in_blanks"] = (3, 4, 6, 3, 4, 5),
        ["select_missing_word"] = (1, 1, 1, 3, 4, 5),
        ["highlight_correct_summary"] = (1, 1, 1, 3, 4, 5),
        ["highlight_incorrect_words"] = (2, 3, 4, 0, 0, 0),
        ["write_from_dictation"] = (2, 3, 5, 0, 0, 0),
        ["summarize_spoken_text"] = (1, 1, 1, 0, 0, 0),
        // Speaking (multi-item)
        ["answer_short_question"] = (3, 5, 8, 0, 0, 0),
        ["repeat_sentence"] = (3, 5, 6, 0, 0, 0),
        // Speaking (single-substantial-task or small-batch)
        ["read_aloud"] = (1, 2, 3, 0, 0, 0),
        ["describe_image"] = (1, 1, 1, 0, 0, 0),
        ["respond_to_situation"] = (1, 1, 2, 0, 0, 0),
        ["retell_lecture"] = (1, 1, 1, 0, 0, 0),
        ["summarize_group_discussion"] = (1, 1, 1, 0, 0, 0),
    };

    public static IReadOnlyList<ExerciseTypeDefinition> CreateDefinitions()
    {
        var definitions = BuildBaseDefinitions();
        foreach (var def in definitions)
        {
            if (CountOverrides.TryGetValue(def.Key, out var c))
                def.UpdateItemCounts(c.MinItems, c.DefItems, c.MaxItems, c.MinOpts, c.DefOpts, c.MaxOpts);
        }
        return definitions;
    }

    private static IReadOnlyList<ExerciseTypeDefinition> BuildBaseDefinitions() =>
    [
        Ready("listening_comprehension", "Listening Comprehension", "Legacy listening activity with workplace audio and questions.", "listening", "[]", "Legacy", "listening", "listening_comprehension", "activity_generate_listening", ActivityType.ListeningComprehension, null, 8, true, false),
        Ready("writing_scenario", "Writing Scenario", "Legacy workplace writing activity.", "writing", "[]", "Legacy", "writing", "writing_scenario", "activity_generate_writing", ActivityType.WritingScenario, null, 8, false, false),
        Ready("speaking_roleplay", "Speaking Roleplay", "Legacy speaking roleplay activity.", "speaking", "[]", "Legacy", "speaking", "speaking_roleplay", "activity_generate_speaking_roleplay", ActivityType.SpeakingRolePlay, null, 6, false, false),
        Ready("vocabulary_practice", "Vocabulary Practice", "Staged vocabulary practice from saved student vocabulary.", "vocabulary", "[\"reading\",\"writing\"]", "Legacy", "vocabulary", "vocabulary_practice", "", ActivityType.VocabularyPractice, null, 5, false, false),
        // Phase K16 — phrase_match now has a real Lesson-generation composer (decomposed into N
        // single_choice sub-questions, see ActivityGenerationService.ComposePhraseMatchAsync),
        // same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.PhraseMatch, "Phrase Match",
            "Match each term to its correct meaning — one radio question per term, options drawn from every pulled term's own definition. Vocabulary/Grammar resources only.",
            "vocabulary", "[]", 2, 5, 8),
        Ready(ExercisePatternKey.GapFillWorkplacePhrase, "Gap Fill Workplace Phrase", "Fill missing words in workplace phrases.", "vocabulary", "[]", "Pattern", "gap_fill", "exact_match", "activity_generate_gap_fill_workplace_phrase", ActivityType.VocabularyPractice, ExercisePatternKey.GapFillWorkplacePhrase, 4, false, false),
        Ready(ExercisePatternKey.ListenAndAnswer, "Listen and Answer", "Answer questions after workplace audio.", "listening", "[]", "Pattern", "audio_and_free_text", "ai_structured", "activity_generate_listen_and_answer", ActivityType.ListeningComprehension, ExercisePatternKey.ListenAndAnswer, 4, true, false),
        Ready(ExercisePatternKey.ListenAndGapFill, "Listen and Gap Fill", "Fill gaps from workplace audio.", "listening", "[\"writing\"]", "Pattern", "audio_and_gap_fill", "exact_match", "activity_generate_listen_and_gap_fill", ActivityType.ListeningComprehension, ExercisePatternKey.ListenAndGapFill, 4, true, false),
        // Phase K17 — email_reply now has a real Lesson-generation composer
        // (ActivityGenerationService.ComposeWritingPrompt), so this row moves from the
        // disabled-by-default Pattern bucket into BankFirst/enabled, same key.
        BankFirst(ExercisePatternKey.EmailReply, "Email Reply",
            "Open-ended email reply task, shown from the resource's own prompt text verbatim. Requires manual or AI evaluation. Writing resources only.",
            "writing", "[]", 1, 1, 1),
        Ready(ExercisePatternKey.TeamsChatSimulation, "Teams Chat Simulation", "Write a concise workplace chat response.", "writing", "[]", "Pattern", "chat_reply", "ai_structured", "activity_generate_teams_chat_simulation", ActivityType.WritingScenario, ExercisePatternKey.TeamsChatSimulation, 5, false, false),
        // Phase K18 — spoken_response_from_prompt now has a real Lesson-generation composer
        // (ActivityGenerationService.ComposeSpeakingPrompt), same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.SpokenResponseFromPrompt, "Spoken Response From Prompt",
            "Open-ended spoken response task, shown from the resource's own prompt text verbatim. Requires manual or AI evaluation. Speaking resources only.",
            "speaking", "[]", 1, 1, 1),
        // Phase K17 — open_writing_task now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as email_reply above, same key.
        BankFirst(ExercisePatternKey.OpenWritingTask, "Open Writing Task",
            "Open-ended writing task, shown from the resource's own prompt text verbatim. Requires manual or AI evaluation. Writing resources only.",
            "writing", "[]", 1, 1, 1),
        // Phase K18 — speaking_roleplay_turn now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as spoken_response_from_prompt above, same key.
        BankFirst(ExercisePatternKey.SpeakingRoleplayTurn, "Speaking Roleplay Turn",
            "Open-ended roleplay turn, shown from the resource's own prompt text verbatim. Requires manual or AI evaluation. Speaking resources only.",
            "speaking", "[]", 1, 1, 1),
        // Phase K19 — lesson_reflection now has a real Lesson-generation composer, sourced from
        // the Lesson's own Body/Title rather than a Resource Bank row (see
        // ActivityGenerationService.ComposeAndSaveLessonReflectionAsync). Same key, moves to
        // BankFirst/enabled. Not skill-gated in the frontend picker — reflection applies to any
        // Lesson regardless of its skill.
        BankFirst(ExercisePatternKey.LessonReflection, "Lesson Reflection",
            "Open-ended reflection prompt generated from the Lesson's own body text, not a Resource Bank row. Requires manual or AI evaluation. Available for any Lesson.",
            "reflection", "[]", 1, 1, 1),
        // Phase K17 — reading_multiple_choice_single now has a real (AI-assisted)
        // Lesson-generation composer (AiExerciseGenerationService.ComposeReadingMultipleChoiceSingle),
        // so this row moves from the disabled-by-default Pattern bucket into BankFirst/enabled,
        // same key.
        BankFirst(ExercisePatternKey.ReadingMultipleChoiceSingle, "Reading Multiple Choice (Single Answer)",
            "AI-generated comprehension question with a single correct answer, judged from the resource's own excerpt/passage text. ReadingReference/ReadingPassage resources only.",
            "reading", "[]", 1, 1, 1),
        // Phase K17 — reading_multiple_choice_multi now has a real (AI-assisted)
        // Lesson-generation composer (AiExerciseGenerationService.ComposeReadingMultipleChoiceMulti),
        // so this row moves from the disabled-by-default Pattern bucket into BankFirst/enabled,
        // same key.
        BankFirst(ExercisePatternKey.ReadingMultipleChoiceMulti, "Reading Multiple Choice (Multiple Answers)",
            "AI-generated comprehension question with 2+ correct answers, judged from the resource's own excerpt/passage text. ReadingReference/ReadingPassage resources only.",
            "reading", "[]", 1, 1, 1),
        // Phase K16 — reading_fill_in_blanks now has a real Lesson-generation composer
        // (ActivityGenerationService.ComposeReadingFillInBlanksAsync), so this row moves from the
        // disabled-by-default Pattern bucket into BankFirst/enabled, same key.
        BankFirst(ExercisePatternKey.ReadingFillInBlanks, "Reading Fill in Blanks",
            "Fill in numbered blanks from the resource's own excerpt/passage text. ReadingReference/ReadingPassage resources only.",
            "reading", "[]", 3, 4, 6),
        // Phase K16 — reorder_paragraphs now has a real Lesson-generation composer (stock Form.io
        // datagrid+reorder pattern, see ActivityGenerationService.ComposeReorderParagraphsAsync),
        // same key, moves to BankFirst/enabled. Flagged for manual browser verification — the
        // datagrid defaultValue pre-population behavior was never confirmed live.
        BankFirst(ExercisePatternKey.ReorderParagraphs, "Reorder Paragraphs",
            "Drag shuffled paragraphs from the resource's own passage text into the correct order. ReadingPassage resources only (needs multi-paragraph text).",
            "reading", "[]", 3, 4, 6),

        // Phase K18 — read_aloud now has a real Lesson-generation composer, same BankFirst/enabled
        // conversion as the other Speaking types, same key.
        BankFirst(ExercisePatternKey.ReadAloud, "Read Aloud",
            "Read the resource's own prompt text aloud as clearly and naturally as possible. Requires manual or AI evaluation. Speaking resources only.",
            "speaking", "[\"pronunciation\", \"reading\"]", 1, 1, 1),
        // read_aloud promoted to Ready above
        Ready(ExercisePatternKey.RepeatSentence, "Repeat Sentence", "Hear or read a short sentence, then repeat it as accurately as you can.", "speaking", "[\"listening\", \"pronunciation\"]", "Pattern", "repeat_sentence", "exact_match", "activity_generate_repeat_sentence", ActivityType.SpeakingRolePlay, ExercisePatternKey.RepeatSentence, 5, false, false),
        // repeat_sentence promoted to Ready above
        // Phase K20 — describe_image now has a real Lesson-generation composer, sourced from
        // the Speaking resource's new optional ImageUrl field (must be set on that resource in
        // the Resource Bank edit page first). Same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.DescribeImage, "Describe Image",
            "Look at the resource's own image and describe what you see aloud. Requires the Speaking resource to have an Image URL set. Requires manual or AI evaluation.",
            "speaking", "[\"vocabulary\", \"communication\"]", 1, 1, 1),
        // describe_image promoted to Ready above
        // Phase K18 — respond_to_situation now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as the other Speaking types, same key.
        BankFirst(ExercisePatternKey.RespondToSituation, "Respond to Situation",
            "Read a real-life situation from the resource's own prompt text and speak an appropriate response. Requires manual or AI evaluation. Speaking resources only.",
            "speaking", "[\"communication\", \"listening\"]", 1, 1, 1),
        // respond_to_situation promoted to Ready above
        // Phase K18 — retell_lecture now has a real Lesson-generation composer (reuses
        // ComposeSpeakingPrompt against the transcript), same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.RetellLecture, "Retell Lecture",
            "Read the resource's own transcript and retell the main ideas aloud, in your own words. Requires manual or AI evaluation. Listening resources only.",
            "listening", "[\"speaking\", \"summarizing\", \"communication\"]", 1, 1, 1),
        // retell_lecture promoted to Ready above
        // Phase K18 — summarize_group_discussion now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as retell_lecture above, same key.
        BankFirst(ExercisePatternKey.SummarizeGroupDiscussion, "Summarize Group Discussion",
            "Read the resource's own transcript and summarize the main points, speaker views, and outcomes aloud. Requires manual or AI evaluation. Listening resources only.",
            "listening", "[\"speaking\", \"summarizing\", \"communication\"]", 1, 1, 1),
        // summarize_group_discussion promoted to Ready above
        // Phase K18 — answer_short_question now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as the other Speaking types, same key.
        BankFirst(ExercisePatternKey.AnswerShortQuestion, "Answer Short Question",
            "Read the resource's own question text and speak your answer clearly. Requires manual or AI evaluation. Speaking resources only.",
            "speaking", "[\"listening\"]", 1, 1, 1),
        // Phase K17 — summarize_written_text now has a real Lesson-generation composer
        // (ActivityGenerationService.ComposeSummarizeWrittenText), so this row moves from the
        // disabled-by-default Pattern bucket into BankFirst/enabled, same key. Writing-skill but
        // Reading-resource-sourced (see ExerciseGenerationService's doc comment).
        BankFirst(ExercisePatternKey.SummarizeWrittenText, "Summarize Written Text",
            "Open-ended summary task over the resource's own excerpt/passage text. Requires manual or AI evaluation. ReadingReference/ReadingPassage resources only.",
            "writing", "[\"reading\"]", 1, 1, 1),
        // Phase K17 — write_essay now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as email_reply/open_writing_task above, same key.
        BankFirst(ExercisePatternKey.WriteEssay, "Write Essay",
            "Structured essay response, shown from the resource's own prompt text verbatim. Requires manual or AI evaluation. Writing resources only.",
            "writing", "[]", 1, 1, 1),
        // Phase K18 — reading_writing_fill_in_blanks now has a real Lesson-generation composer
        // (ActivityGenerationService.ComposeReadingWritingFillInBlanksAsync), same key, moves to
        // BankFirst/enabled.
        BankFirst(ExercisePatternKey.ReadingWritingFillInBlanks, "Reading and Writing Fill in Blanks",
            "Choose the correct word for each numbered blank from radio options (correct word + 2 distractors drawn from the same text). ReadingReference/ReadingPassage resources only.",
            "reading", "[\"writing\"]", 3, 4, 6),
        // Phase K18 — summarize_spoken_text now has a real Lesson-generation composer (reuses
        // ComposeWritingPrompt against the transcript), same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.SummarizeSpokenText, "Summarize Spoken Text",
            "Read the resource's own transcript and write a concise summary in your own words. Requires manual or AI evaluation. Listening resources only.",
            "listening", "[\"writing\"]", 1, 1, 1),
        // summarize_spoken_text promoted to Ready above
        // Phase K17 — listening_multiple_choice_multi now has a real (AI-assisted)
        // Lesson-generation composer, same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.ListeningMultipleChoiceMulti, "Listening Multiple Choice (Multiple Answers)",
            "AI-generated comprehension question with 2+ correct answers, judged from the resource's own transcript. Listening resources only.",
            "listening", "[]", 1, 1, 1),
        // Phase K17 — listening_fill_in_blanks now has a real (deterministic) Lesson-generation
        // composer (ActivityGenerationService.ComposeListeningFillInBlanks), same key, moves to
        // BankFirst/enabled.
        BankFirst(ExercisePatternKey.ListeningFillInBlanks, "Listening Fill in Blanks",
            "Fill in numbered blanks from the resource's own transcript. Listening resources only.",
            "listening", "[\"writing\"]", 3, 4, 6),
        // Phase K17 — highlight_correct_summary now has a real (AI-assisted) Lesson-generation
        // composer (reuses AiExerciseGenerationService.ComposeReadingMultipleChoiceSingle), same
        // key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.HighlightCorrectSummary, "Highlight Correct Summary",
            "AI-generated summary-selection question — pick the one-sentence summary that best matches the resource's own transcript. Listening resources only.",
            "listening", "[\"reading\"]", 1, 1, 1),
        // highlight_correct_summary promoted to Ready above
        // Phase K17 — listening_multiple_choice_single now has a real (AI-assisted)
        // Lesson-generation composer, same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.ListeningMultipleChoiceSingle, "Listening Multiple Choice (Single Answer)",
            "AI-generated comprehension question with a single correct answer, judged from the resource's own transcript. Listening resources only.",
            "listening", "[]", 1, 1, 1),
        // Phase K17 — select_missing_word now has a real Lesson-generation composer
        // (ActivityGenerationService.PickBlankWord + AiExerciseGenerationService.ComposeSelectMissingWord)
        // — the correct answer is deterministic (a real transcript word), AI only supplies
        // wrong-word distractors. Same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.SelectMissingWord, "Select Missing Word",
            "Choose the word that completes the blank in the resource's own transcript — the correct word is picked deterministically, AI only supplies wrong-word distractors. Listening resources only.",
            "listening", "[]", 1, 1, 1),
        // select_missing_word promoted to Ready above
        Ready(ExercisePatternKey.HighlightIncorrectWords, "Highlight Incorrect Words", "Listen to a short audio script and select the transcript words that differ from it.", "listening", "[\"reading\"]", "Pattern", "highlight_incorrect_words", "keyed_selection", "activity_generate_highlight_incorrect_words", ActivityType.ListeningComprehension, ExercisePatternKey.HighlightIncorrectWords, 5, false, false),
        // highlight_incorrect_words promoted to Ready above
        Ready(ExercisePatternKey.WriteFromDictation, "Write From Dictation", "Listen to short audio clips and type exactly what you hear.", "listening", "[\"writing\"]", "Pattern", "write_from_dictation", "exact_match", "activity_generate_write_from_dictation", ActivityType.ListeningComprehension, ExercisePatternKey.WriteFromDictation, 5, false, false),
        // write_from_dictation promoted to Ready above

        // Form.io Practice Gym pilot — "planned" (not "ready") so it is never queued or
        // materialized until an admin explicitly promotes it. See
        // docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md.
        Planned(ExercisePatternKey.FormIoPracticeGymPilot, "Form.io Practice Gym Pilot", "Pilot: ActivityTemplate-personalized Form.io schema rendered and scored deterministically in Practice Gym.", "speaking", "[]", "Pilot"),

        // Phase K15 — the 3 types actually wired to Lesson "Generate Exercises" today (see
        // ActivityGenerationService/AiExerciseGenerationService). Enabled by default; every other
        // entry above stays disabled until its own composer ships (K16-K19).
        BankFirst("gap_fill", "Gap Fill", "Type the missing term from its definition. Vocabulary/Grammar resources only.", "vocabulary", "[\"grammar\"]", 1, 1, 1),
        BankFirst("multiple_choice_single", "Multiple Choice (Single Answer)", "Choose the correct meaning from AI-generated plausible-but-wrong options. Vocabulary/Grammar resources only.", "vocabulary", "[\"grammar\"]", 1, 1, 1),
        BankFirst("short_answer", "Short Answer", "Open-ended comprehension question over a passage/reference excerpt, requires manual or AI evaluation. Reading resources only.", "reading", "[]", 1, 1, 1),
    ];

    public static async Task SeedAsync(LinguaCoachDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existing = await db.ExerciseTypeDefinitions.ToDictionaryAsync(e => e.Key, ct);
        var added = 0;
        var updated = 0;
        foreach (var definition in CreateDefinitions())
        {
            if (!existing.TryGetValue(definition.Key, out var current))
            {
                db.ExerciseTypeDefinitions.Add(definition);
                added++;
                continue;
            }
            current.SyncCatalogMetadata(definition);
            updated++;
        }
        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Added} and synced {Updated} exercise type definitions.", added, updated);
        }
    }

    // Phase K15 — none of these 37 legacy/pattern-catalog entries have a real Lesson-generation
    // composer yet (see docs/sprints/exercise-type-catalog-lesson-generation-buildout-sprint.md);
    // seeded disabled so the Lesson "Generate Exercises" picker (catalog-driven as of K15) never
    // offers a type the backend would 400 on. Flip to enabled per-type as each K16-K19 composer
    // ships — that's the whole point of Enabled now meaning "usable for Lesson generation".
    private static ExerciseTypeDefinition Ready(string key, string name, string description, string primarySkill, string secondarySkillsJson, string category, string rendererKey, string evaluatorKey, string promptKey, ActivityType? activityType, string? patternKey, int minutes, bool requiresAudio, bool requiresImage) =>
        new(key, name, description, primarySkill, secondarySkillsJson, category, false, "ready", rendererKey, evaluatorKey, promptKey, activityType, patternKey, minutes, requiresAudio, requiresImage);

    private static ExerciseTypeDefinition Planned(string key, string name, string description, string primarySkill, string secondarySkillsJson, string category, bool requiresAudio = false, bool requiresImage = false) =>
        new(key, name, description, primarySkill, secondarySkillsJson, category, false, "planned", key, key, $"activity_generate_{key}", null, null, 8, requiresAudio, requiresImage);

    /// <summary>The 3 activity types <see cref="LinguaCoach.Infrastructure.Exercises.ActivityGenerationService"/>
    /// and <see cref="LinguaCoach.Infrastructure.Exercises.AiExerciseGenerationService"/> actually implement
    /// today — these are the only catalog entries enabled by default, and are what the Lesson
    /// "Generate Exercises" picker resolves against (Category = "BankFirst").</summary>
    private static ExerciseTypeDefinition BankFirst(string key, string name, string description, string primarySkill, string secondarySkillsJson, int minItems, int defItems, int maxItems) =>
        new(key, name, description, primarySkill, secondarySkillsJson, "BankFirst", true, "ready",
            "formio", "deterministic_or_ai", "", null, null, 3, false, false,
            minItems, defItems, maxItems, 0, 0, 0);
}
