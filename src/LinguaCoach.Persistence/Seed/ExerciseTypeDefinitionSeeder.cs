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
        Ready(ExercisePatternKey.PhraseMatch, "Phrase Match", "Match workplace phrases to meanings.", "vocabulary", "[]", "Pattern", "matching_pairs", "keyed_selection", "activity_generate_phrase_match", ActivityType.VocabularyPractice, ExercisePatternKey.PhraseMatch, 3, false, false),
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
        Ready(ExercisePatternKey.SpokenResponseFromPrompt, "Spoken Response From Prompt", "Respond aloud to a workplace prompt.", "speaking", "[]", "Pattern", "free_text_entry", "ai_open_ended", "activity_generate_spoken_response_from_prompt", ActivityType.SpeakingRolePlay, ExercisePatternKey.SpokenResponseFromPrompt, 5, false, false),
        // Phase K17 — open_writing_task now has a real Lesson-generation composer, same
        // BankFirst/enabled conversion as email_reply above, same key.
        BankFirst(ExercisePatternKey.OpenWritingTask, "Open Writing Task",
            "Open-ended writing task, shown from the resource's own prompt text verbatim. Requires manual or AI evaluation. Writing resources only.",
            "writing", "[]", 1, 1, 1),
        Ready(ExercisePatternKey.SpeakingRoleplayTurn, "Speaking Roleplay Turn", "Record one spoken workplace roleplay turn.", "speaking", "[]", "Pattern", "audio_response", "ai_open_ended", "activity_generate_speaking_roleplay_turn", ActivityType.SpeakingRolePlay, ExercisePatternKey.SpeakingRoleplayTurn, 5, false, false),
        Ready(ExercisePatternKey.LessonReflection, "Lesson Reflection", "Review and reflect on the lesson.", "reflection", "[]", "Pattern", "read_only", "no_marking", "activity_generate_lesson_reflection", ActivityType.WritingScenario, ExercisePatternKey.LessonReflection, 2, false, false),
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
        Ready(ExercisePatternKey.ReorderParagraphs, "Reorder Paragraphs", "Put paragraphs in the correct logical order.", "reading", "[]", "Pattern", "reorder_paragraphs", "exact_match", "activity_generate_reorder_paragraphs", ActivityType.ReadingTask, ExercisePatternKey.ReorderParagraphs, 5, false, false),

        Ready(ExercisePatternKey.ReadAloud, "Read Aloud", "Read a short workplace text aloud as clearly and naturally as possible.", "speaking", "[\"pronunciation\", \"reading\"]", "Pattern", "read_aloud", "exact_match", "activity_generate_read_aloud", ActivityType.SpeakingRolePlay, ExercisePatternKey.ReadAloud, 5, false, false),
        // read_aloud promoted to Ready above
        Ready(ExercisePatternKey.RepeatSentence, "Repeat Sentence", "Hear or read a short sentence, then repeat it as accurately as you can.", "speaking", "[\"listening\", \"pronunciation\"]", "Pattern", "repeat_sentence", "exact_match", "activity_generate_repeat_sentence", ActivityType.SpeakingRolePlay, ExercisePatternKey.RepeatSentence, 5, false, false),
        // repeat_sentence promoted to Ready above
        Ready(ExercisePatternKey.DescribeImage, "Describe Image", "Look at an image prompt and describe what you see as clearly and naturally as possible.", "speaking", "[\"vocabulary\", \"communication\"]", "Pattern", "describe_image", "ai_open_ended", "activity_generate_describe_image", ActivityType.SpeakingRolePlay, ExercisePatternKey.DescribeImage, 6, false, false),
        // describe_image promoted to Ready above
        Ready(ExercisePatternKey.RespondToSituation, "Respond to Situation", "Read or hear a short real-life situation and speak an appropriate response.", "speaking", "[\"communication\", \"listening\"]", "Pattern", "respond_to_situation", "ai_open_ended", "activity_generate_respond_to_situation", ActivityType.SpeakingRolePlay, ExercisePatternKey.RespondToSituation, 6, false, false),
        // respond_to_situation promoted to Ready above
        Ready(ExercisePatternKey.RetellLecture, "Retell Lecture", "Listen to or read a short lecture and retell the main ideas in your own words.", "listening", "[\"speaking\", \"summarizing\", \"communication\"]", "Pattern", "retell_lecture", "ai_open_ended", "activity_generate_retell_lecture", ActivityType.SpeakingRolePlay, ExercisePatternKey.RetellLecture, 7, false, false),
        // retell_lecture promoted to Ready above
        Ready(ExercisePatternKey.SummarizeGroupDiscussion, "Summarize Group Discussion", "Listen to or read a short multi-speaker discussion and summarize the main points, speaker views, agreements, and outcomes.", "listening", "[\"speaking\", \"summarizing\", \"communication\"]", "Pattern", "summarize_group_discussion", "ai_open_ended", "activity_generate_summarize_group_discussion", ActivityType.SpeakingRolePlay, ExercisePatternKey.SummarizeGroupDiscussion, 7, false, false),
        // summarize_group_discussion promoted to Ready above
        Ready(ExercisePatternKey.AnswerShortQuestion, "Answer Short Question", "Listen to short questions and speak your answers clearly.", "speaking", "[\"listening\"]", "Pattern", "answer_short_question", "exact_match", "activity_generate_answer_short_question", ActivityType.SpeakingRolePlay, ExercisePatternKey.AnswerShortQuestion, 6, false, false),
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
        Ready(ExercisePatternKey.ReadingWritingFillInBlanks, "Reading and Writing Fill in Blanks", "Choose the correct word for each blank in a reading passage.", "reading", "[\"writing\"]", "Pattern", "reading_writing_fill_in_blanks", "exact_match", "activity_generate_reading_writing_fill_in_blanks", ActivityType.ReadingTask, ExercisePatternKey.ReadingWritingFillInBlanks, 5, false, false),
        // reading_writing_fill_in_blanks promoted to Ready above
        // reading_multiple_choice_multi promoted to Ready above
        // reorder_paragraphs promoted to Ready above
        // reading_fill_in_blanks promoted to Ready above
        Ready(ExercisePatternKey.SummarizeSpokenText, "Summarize Spoken Text", "Listen to a short spoken text and write a concise summary in your own words.", "listening", "[\"writing\"]", "Pattern", "summarize_spoken_text", "ai_structured", "activity_generate_summarize_spoken_text", ActivityType.ListeningComprehension, ExercisePatternKey.SummarizeSpokenText, 6, true, false),
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
        Ready(ExercisePatternKey.HighlightCorrectSummary, "Highlight Correct Summary", "Listen to a short audio script and choose the summary that best matches it.", "listening", "[\"reading\"]", "Pattern", "highlight_correct_summary", "keyed_selection", "activity_generate_highlight_correct_summary", ActivityType.ListeningComprehension, ExercisePatternKey.HighlightCorrectSummary, 5, false, false),
        // highlight_correct_summary promoted to Ready above
        // Phase K17 — listening_multiple_choice_single now has a real (AI-assisted)
        // Lesson-generation composer, same key, moves to BankFirst/enabled.
        BankFirst(ExercisePatternKey.ListeningMultipleChoiceSingle, "Listening Multiple Choice (Single Answer)",
            "AI-generated comprehension question with a single correct answer, judged from the resource's own transcript. Listening resources only.",
            "listening", "[]", 1, 1, 1),
        Ready(ExercisePatternKey.SelectMissingWord, "Select Missing Word", "Listen to an audio script and choose the word or phrase that completes it.", "listening", "[]", "Pattern", "select_missing_word", "keyed_selection", "activity_generate_select_missing_word", ActivityType.ListeningComprehension, ExercisePatternKey.SelectMissingWord, 5, false, false),
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
