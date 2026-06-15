using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

public static class ExerciseTypeDefinitionSeeder
{
    public static IReadOnlyList<ExerciseTypeDefinition> CreateDefinitions() =>
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

        Planned("read_aloud", "Read Aloud", "Read text aloud clearly.", "speaking", "[\"reading\"]", "Planned speaking format", true, false),
        Planned("repeat_sentence", "Repeat Sentence", "Repeat an audio sentence accurately.", "speaking", "[\"listening\"]", "Planned speaking format", true, false, requiresAudio: true),
        Planned("describe_image", "Describe Image", "Describe an image aloud.", "speaking", "[]", "Planned speaking format", true, false, requiresImage: true),
        Planned("respond_to_situation", "Respond to Situation", "Speak a response to a workplace situation.", "speaking", "[]", "Planned speaking format", true, false),
        Planned("retell_lecture", "Retell Lecture", "Retell an audio lecture in your own words.", "speaking", "[\"listening\"]", "Planned speaking format", true, false, requiresAudio: true),
        Planned("summarize_group_discussion", "Summarize Group Discussion", "Summarize a spoken discussion.", "speaking", "[\"listening\"]", "Planned speaking format", true, false, requiresAudio: true),
        Planned("answer_short_question", "Answer Short Question", "Answer a short spoken question.", "speaking", "[\"listening\"]", "Planned speaking format", true, false, requiresAudio: true),
        Planned("summarize_written_text", "Summarize Written Text", "Write a concise summary of a passage.", "writing", "[\"reading\"]", "Planned reading/writing format", true, false),
        Planned("write_essay", "Write Essay", "Write a structured essay.", "writing", "[]", "Planned reading/writing format", true, false),
        Planned("reading_writing_fill_in_blanks", "Reading and Writing Fill in Blanks", "Choose words for text blanks.", "reading", "[\"writing\"]", "Planned reading/writing format", true, false),
        // reading_multiple_choice_multi promoted to Ready above
        // reorder_paragraphs promoted to Ready above
        // reading_fill_in_blanks promoted to Ready above
        Planned("summarize_spoken_text", "Summarize Spoken Text", "Write a summary of spoken audio.", "listening", "[\"writing\"]", "Planned listening format", true, false, requiresAudio: true),
        Planned("listening_multiple_choice_multi", "Listening Multiple Choice Multiple", "Choose multiple answers from audio.", "listening", "[]", "Planned listening format", true, false, requiresAudio: true),
        Planned("listening_fill_in_blanks", "Listening Fill in Blanks", "Fill missing words from audio.", "listening", "[\"writing\"]", "Planned listening format", true, false, requiresAudio: true),
        Planned("highlight_correct_summary", "Highlight Correct Summary", "Choose the correct audio summary.", "listening", "[\"reading\"]", "Planned listening format", true, false, requiresAudio: true),
        Planned("listening_multiple_choice_single", "Listening Multiple Choice Single", "Choose one answer from audio.", "listening", "[]", "Planned listening format", true, false, requiresAudio: true),
        Planned("select_missing_word", "Select Missing Word", "Select the missing audio ending.", "listening", "[]", "Planned listening format", true, false, requiresAudio: true),
        Planned("highlight_incorrect_words", "Highlight Incorrect Words", "Find words that differ from audio.", "listening", "[\"reading\"]", "Planned listening format", true, false, requiresAudio: true),
        Planned("write_from_dictation", "Write From Dictation", "Write the sentence you hear.", "listening", "[\"writing\"]", "Planned listening format", true, false, requiresAudio: true)
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
