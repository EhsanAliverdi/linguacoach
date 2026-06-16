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
    private static readonly Dictionary<string, (int MinItems, int DefItems, int MaxItems, int MinOpts, int DefOpts, int MaxOpts)> CountOverrides = new(StringComparer.Ordinal)
    {
        // Reading
        ["reading_multiple_choice_single"] = (1, 1, 1, 3, 4, 5),
        ["reading_multiple_choice_multi"] = (1, 1, 1, 4, 4, 6),
        ["reading_fill_in_blanks"] = (3, 4, 6, 3, 4, 5),
        ["reading_writing_fill_in_blanks"] = (3, 4, 6, 3, 4, 5),
        ["reorder_paragraphs"] = (4, 4, 5, 0, 0, 0),
        // Writing
        ["summarize_written_text"] = (1, 1, 1, 0, 0, 0),
        ["write_essay"] = (1, 1, 1, 0, 0, 0),
        // Listening
        ["listening_multiple_choice_single"] = (1, 1, 1, 3, 4, 5),
        ["listening_multiple_choice_multi"] = (1, 1, 1, 4, 4, 6),
        ["listening_fill_in_blanks"] = (3, 4, 6, 3, 4, 5),
        ["select_missing_word"] = (1, 1, 1, 3, 4, 5),
        ["highlight_correct_summary"] = (1, 1, 1, 3, 4, 5),
        ["highlight_incorrect_words"] = (2, 3, 4, 0, 0, 0),
        ["write_from_dictation"] = (2, 3, 5, 0, 0, 0),
        ["summarize_spoken_text"] = (1, 1, 1, 0, 0, 0),
        // Speaking (planned, non-runnable)
        ["answer_short_question"] = (3, 5, 8, 0, 0, 0),
        ["repeat_sentence"] = (3, 5, 6, 0, 0, 0),
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
        Ready("listening_comprehension", "Listening Comprehension", "Legacy listening activity with workplace audio and questions.", "listening", "[]", "Legacy", "listening", "listening_comprehension", "activity_generate_listening", ActivityType.ListeningComprehension, null, 8, true, false, false, true),
        Ready("writing_scenario", "Writing Scenario", "Legacy workplace writing activity.", "writing", "[]", "Legacy", "writing", "writing_scenario", "activity_generate_writing", ActivityType.WritingScenario, null, 8, false, false, true, true),
        Ready("speaking_roleplay", "Speaking Roleplay", "Legacy speaking roleplay activity.", "speaking", "[]", "Legacy", "speaking", "speaking_roleplay", "activity_generate_speaking_roleplay", ActivityType.SpeakingRolePlay, null, 6, false, false, true, true),
        Ready("vocabulary_practice", "Vocabulary Practice", "Staged vocabulary practice from saved student vocabulary.", "vocabulary", "[\"reading\",\"writing\"]", "Legacy", "vocabulary", "vocabulary_practice", "", ActivityType.VocabularyPractice, null, 5, false, false, true, true),
        Ready(ExercisePatternKey.PhraseMatch, "Phrase Match", "Match workplace phrases to meanings.", "vocabulary", "[]", "Pattern", "matching_pairs", "keyed_selection", "activity_generate_phrase_match", ActivityType.VocabularyPractice, ExercisePatternKey.PhraseMatch, 3, false, false, true, true),
        Ready(ExercisePatternKey.GapFillWorkplacePhrase, "Gap Fill Workplace Phrase", "Fill missing words in workplace phrases.", "vocabulary", "[]", "Pattern", "gap_fill", "exact_match", "activity_generate_gap_fill_workplace_phrase", ActivityType.VocabularyPractice, ExercisePatternKey.GapFillWorkplacePhrase, 4, false, false, true, true),
        Ready(ExercisePatternKey.ListenAndAnswer, "Listen and Answer", "Answer questions after workplace audio.", "listening", "[]", "Pattern", "audio_and_free_text", "ai_structured", "activity_generate_listen_and_answer", ActivityType.ListeningComprehension, ExercisePatternKey.ListenAndAnswer, 4, true, false, true, true),
        Ready(ExercisePatternKey.ListenAndGapFill, "Listen and Gap Fill", "Fill gaps from workplace audio.", "listening", "[\"writing\"]", "Pattern", "audio_and_gap_fill", "exact_match", "activity_generate_listen_and_gap_fill", ActivityType.ListeningComprehension, ExercisePatternKey.ListenAndGapFill, 4, true, false, true, true),
        Ready(ExercisePatternKey.EmailReply, "Email Reply", "Write a workplace email reply.", "writing", "[]", "Pattern", "email_reply", "ai_structured", "activity_generate_email_reply", ActivityType.WritingScenario, ExercisePatternKey.EmailReply, 7, false, false, true, true),
        Ready(ExercisePatternKey.TeamsChatSimulation, "Teams Chat Simulation", "Write a concise workplace chat response.", "writing", "[]", "Pattern", "chat_reply", "ai_structured", "activity_generate_teams_chat_simulation", ActivityType.WritingScenario, ExercisePatternKey.TeamsChatSimulation, 5, false, false, true, true),
        Ready(ExercisePatternKey.SpokenResponseFromPrompt, "Spoken Response From Prompt", "Respond aloud to a workplace prompt.", "speaking", "[]", "Pattern", "free_text_entry", "ai_open_ended", "activity_generate_spoken_response_from_prompt", ActivityType.SpeakingRolePlay, ExercisePatternKey.SpokenResponseFromPrompt, 5, false, false, true, true),
        Ready(ExercisePatternKey.OpenWritingTask, "Open Writing Task", "Open workplace writing with coaching feedback.", "writing", "[]", "Pattern", "free_text_entry", "ai_open_ended", "activity_generate_open_writing_task", ActivityType.WritingScenario, ExercisePatternKey.OpenWritingTask, 7, false, false, true, true),
        Ready(ExercisePatternKey.SpeakingRoleplayTurn, "Speaking Roleplay Turn", "Record one spoken workplace roleplay turn.", "speaking", "[]", "Pattern", "audio_response", "ai_open_ended", "activity_generate_speaking_roleplay_turn", ActivityType.SpeakingRolePlay, ExercisePatternKey.SpeakingRoleplayTurn, 5, false, false, true, true),
        Ready(ExercisePatternKey.LessonReflection, "Lesson Reflection", "Review and reflect on the lesson.", "reflection", "[]", "Pattern", "read_only", "no_marking", "activity_generate_lesson_reflection", ActivityType.WritingScenario, ExercisePatternKey.LessonReflection, 2, false, false, false, true),
        Ready(ExercisePatternKey.ReadingMultipleChoiceSingle, "Reading Multiple Choice Single", "Choose one answer from reading.", "reading", "[]", "Pattern", "reading_multiple_choice_single", "keyed_selection", "activity_generate_reading_multiple_choice_single", ActivityType.ReadingTask, ExercisePatternKey.ReadingMultipleChoiceSingle, 5, false, false, true, false),
        Ready(ExercisePatternKey.ReadingMultipleChoiceMulti, "Reading Multiple Choice Multiple", "Choose multiple answers from reading.", "reading", "[]", "Pattern", "reading_multiple_choice_multi", "keyed_selection", "activity_generate_reading_multiple_choice_multi", ActivityType.ReadingTask, ExercisePatternKey.ReadingMultipleChoiceMulti, 5, false, false, true, false),
        Ready(ExercisePatternKey.ReadingFillInBlanks, "Reading Fill in Blanks", "Fill blanks in a reading passage.", "reading", "[]", "Pattern", "reading_fill_in_blanks", "exact_match", "activity_generate_reading_fill_in_blanks", ActivityType.ReadingTask, ExercisePatternKey.ReadingFillInBlanks, 5, false, false, true, false),
        Ready(ExercisePatternKey.ReorderParagraphs, "Reorder Paragraphs", "Put paragraphs in the correct logical order.", "reading", "[]", "Pattern", "reorder_paragraphs", "exact_match", "activity_generate_reorder_paragraphs", ActivityType.ReadingTask, ExercisePatternKey.ReorderParagraphs, 5, false, false, true, false),

        Ready(ExercisePatternKey.ReadAloud, "Read Aloud", "Read a short workplace text aloud as clearly and naturally as possible.", "speaking", "[\"pronunciation\", \"reading\"]", "Pattern", "read_aloud", "exact_match", "activity_generate_read_aloud", ActivityType.SpeakingRolePlay, ExercisePatternKey.ReadAloud, 5, false, false, true, false),
        // read_aloud promoted to Ready above
        Ready(ExercisePatternKey.RepeatSentence, "Repeat Sentence", "Hear or read a short sentence, then repeat it as accurately as you can.", "speaking", "[\"listening\", \"pronunciation\"]", "Pattern", "repeat_sentence", "exact_match", "activity_generate_repeat_sentence", ActivityType.SpeakingRolePlay, ExercisePatternKey.RepeatSentence, 5, false, false, true, false),
        // repeat_sentence promoted to Ready above
        Planned("describe_image", "Describe Image", "Describe an image aloud.", "speaking", "[]", "Planned speaking format", true, false, requiresImage: true),
        Planned("respond_to_situation", "Respond to Situation", "Speak a response to a workplace situation.", "speaking", "[]", "Planned speaking format", true, false),
        Planned("retell_lecture", "Retell Lecture", "Retell an audio lecture in your own words.", "speaking", "[\"listening\"]", "Planned speaking format", true, false, requiresAudio: true),
        Planned("summarize_group_discussion", "Summarize Group Discussion", "Summarize a spoken discussion.", "speaking", "[\"listening\"]", "Planned speaking format", true, false, requiresAudio: true),
        Ready(ExercisePatternKey.AnswerShortQuestion, "Answer Short Question", "Listen to short questions and speak your answers clearly.", "speaking", "[\"listening\"]", "Pattern", "answer_short_question", "exact_match", "activity_generate_answer_short_question", ActivityType.SpeakingRolePlay, ExercisePatternKey.AnswerShortQuestion, 6, false, false, true, false),
        Ready(ExercisePatternKey.SummarizeWrittenText, "Summarize Written Text", "Read a passage and write a concise summary in your own words.", "writing", "[\"reading\"]", "Pattern", "free_text_entry", "ai_structured", "activity_generate_summarize_written_text", ActivityType.WritingScenario, ExercisePatternKey.SummarizeWrittenText, 7, false, false, true, false),
        // summarize_written_text promoted to Ready above
        Ready(ExercisePatternKey.WriteEssay, "Write Essay", "Read an essay prompt and write a structured essay response.", "writing", "[]", "Pattern", "free_text_entry", "ai_structured", "activity_generate_write_essay", ActivityType.WritingScenario, ExercisePatternKey.WriteEssay, 10, false, false, true, false),
        // write_essay promoted to Ready above
        Ready(ExercisePatternKey.ReadingWritingFillInBlanks, "Reading and Writing Fill in Blanks", "Choose the correct word for each blank in a reading passage.", "reading", "[\"writing\"]", "Pattern", "reading_writing_fill_in_blanks", "exact_match", "activity_generate_reading_writing_fill_in_blanks", ActivityType.ReadingTask, ExercisePatternKey.ReadingWritingFillInBlanks, 5, false, false, true, false),
        // reading_writing_fill_in_blanks promoted to Ready above
        // reading_multiple_choice_multi promoted to Ready above
        // reorder_paragraphs promoted to Ready above
        // reading_fill_in_blanks promoted to Ready above
        Ready(ExercisePatternKey.SummarizeSpokenText, "Summarize Spoken Text", "Listen to a short spoken text and write a concise summary in your own words.", "listening", "[\"writing\"]", "Pattern", "summarize_spoken_text", "ai_structured", "activity_generate_summarize_spoken_text", ActivityType.ListeningComprehension, ExercisePatternKey.SummarizeSpokenText, 6, true, false, true, false),
        // summarize_spoken_text promoted to Ready above
        Ready(ExercisePatternKey.ListeningMultipleChoiceMulti, "Listening Multiple Choice Multiple", "Listen to a short audio script and choose all correct answers.", "listening", "[]", "Pattern", "listening_multiple_choice_multi", "keyed_selection", "activity_generate_listening_multiple_choice_multi", ActivityType.ListeningComprehension, ExercisePatternKey.ListeningMultipleChoiceMulti, 5, false, false, true, false),
        // listening_multiple_choice_multi promoted to Ready above
        Ready(ExercisePatternKey.ListeningFillInBlanks, "Listening Fill in Blanks", "Listen to a short audio script and fill in the missing words.", "listening", "[\"writing\"]", "Pattern", "listening_fill_in_blanks", "exact_match", "activity_generate_listening_fill_in_blanks", ActivityType.ListeningComprehension, ExercisePatternKey.ListeningFillInBlanks, 5, false, false, true, false),
        // listening_fill_in_blanks promoted to Ready above
        Ready(ExercisePatternKey.HighlightCorrectSummary, "Highlight Correct Summary", "Listen to a short audio script and choose the summary that best matches it.", "listening", "[\"reading\"]", "Pattern", "highlight_correct_summary", "keyed_selection", "activity_generate_highlight_correct_summary", ActivityType.ListeningComprehension, ExercisePatternKey.HighlightCorrectSummary, 5, false, false, true, false),
        // highlight_correct_summary promoted to Ready above
        Ready(ExercisePatternKey.ListeningMultipleChoiceSingle, "Listening Multiple Choice Single", "Listen to a short audio script and choose one answer.", "listening", "[]", "Pattern", "listening_multiple_choice_single", "keyed_selection", "activity_generate_listening_multiple_choice_single", ActivityType.ListeningComprehension, ExercisePatternKey.ListeningMultipleChoiceSingle, 5, false, false, true, false),
        // listening_multiple_choice_single promoted to Ready above
        Ready(ExercisePatternKey.SelectMissingWord, "Select Missing Word", "Listen to an audio script and choose the word or phrase that completes it.", "listening", "[]", "Pattern", "select_missing_word", "keyed_selection", "activity_generate_select_missing_word", ActivityType.ListeningComprehension, ExercisePatternKey.SelectMissingWord, 5, false, false, true, false),
        // select_missing_word promoted to Ready above
        Ready(ExercisePatternKey.HighlightIncorrectWords, "Highlight Incorrect Words", "Listen to a short audio script and select the transcript words that differ from it.", "listening", "[\"reading\"]", "Pattern", "highlight_incorrect_words", "keyed_selection", "activity_generate_highlight_incorrect_words", ActivityType.ListeningComprehension, ExercisePatternKey.HighlightIncorrectWords, 5, false, false, true, false),
        // highlight_incorrect_words promoted to Ready above
        Ready(ExercisePatternKey.WriteFromDictation, "Write From Dictation", "Listen to short audio clips and type exactly what you hear.", "listening", "[\"writing\"]", "Pattern", "write_from_dictation", "exact_match", "activity_generate_write_from_dictation", ActivityType.ListeningComprehension, ExercisePatternKey.WriteFromDictation, 5, false, false, true, false)
        // write_from_dictation promoted to Ready above
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

    private static ExerciseTypeDefinition Ready(string key, string name, string description, string primarySkill, string secondarySkillsJson, string category, string rendererKey, string evaluatorKey, string promptKey, ActivityType? activityType, string? patternKey, int minutes, bool requiresAudio, bool requiresImage, bool practice, bool today) =>
        new(key, name, description, primarySkill, secondarySkillsJson, category, true, "ready", rendererKey, evaluatorKey, promptKey, activityType, patternKey, minutes, requiresAudio, requiresImage, practice, today);

    private static ExerciseTypeDefinition Planned(string key, string name, string description, string primarySkill, string secondarySkillsJson, string category, bool practice, bool today, bool requiresAudio = false, bool requiresImage = false) =>
        new(key, name, description, primarySkill, secondarySkillsJson, category, true, "planned", key, key, $"activity_generate_{key}", null, null, 8, requiresAudio, requiresImage, practice, today);
}
