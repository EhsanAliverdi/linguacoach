namespace LinguaCoach.Domain;

/// <summary>
/// Canonical string keys for all defined ExercisePatterns.
/// These constants are the authoritative source — ExercisePatternSeeder and
/// SessionDurationTemplates must use them. Never remove or rename a key that
/// exists in the database; add new keys at the bottom only.
/// </summary>
public static class ExercisePatternKey
{
    // ── Vocabulary / Warmup ──────────────────────────────────────────────────
    public const string PhraseMatch               = "phrase_match";
    public const string GapFillWorkplacePhrase    = "gap_fill_workplace_phrase";

    // ── Listening ────────────────────────────────────────────────────────────
    public const string ListenAndAnswer           = "listen_and_answer";
    public const string ListenAndGapFill          = "listen_and_gap_fill";

    // ── Writing / Workplace Communication ────────────────────────────────────
    public const string EmailReply                = "email_reply";
    public const string TeamsChatSimulation       = "teams_chat_simulation";
    public const string OpenWritingTask           = "open_writing_task";

    // ── Speaking ─────────────────────────────────────────────────────────────
    public const string SpokenResponseFromPrompt  = "spoken_response_from_prompt";
    public const string SpeakingRoleplayTurn      = "speaking_roleplay_turn";

    // ── Review / Reflection ──────────────────────────────────────────────────
    public const string LessonReflection          = "lesson_reflection";

    // ── Reading ──────────────────────────────────────────────────────────────
    public const string ReadingMultipleChoiceSingle = "reading_multiple_choice_single";
    public const string ReadingMultipleChoiceMulti  = "reading_multiple_choice_multi";
    public const string ReadingFillInBlanks         = "reading_fill_in_blanks";
    public const string ReorderParagraphs           = "reorder_paragraphs";
    public const string ReadingWritingFillInBlanks  = "reading_writing_fill_in_blanks";

    // ── Reading / Writing ────────────────────────────────────────────────────
    public const string SummarizeWrittenText        = "summarize_written_text";
    public const string WriteEssay                  = "write_essay";

    // ── Listening ────────────────────────────────────────────────────────────
    public const string ListeningMultipleChoiceSingle = "listening_multiple_choice_single";
    public const string ListeningMultipleChoiceMulti  = "listening_multiple_choice_multi";
    public const string ListeningFillInBlanks          = "listening_fill_in_blanks";
    public const string SelectMissingWord              = "select_missing_word";
    public const string HighlightCorrectSummary        = "highlight_correct_summary";
    public const string HighlightIncorrectWords        = "highlight_incorrect_words";
    public const string WriteFromDictation             = "write_from_dictation";
    public const string SummarizeSpokenText            = "summarize_spoken_text";

    // ── Speaking ─────────────────────────────────────────────────────────────
    public const string AnswerShortQuestion            = "answer_short_question";
    public const string ReadAloud                      = "read_aloud";
    public const string RepeatSentence                 = "repeat_sentence";
}
