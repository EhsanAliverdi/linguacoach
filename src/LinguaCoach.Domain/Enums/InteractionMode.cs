namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Determines which frontend renderer component is used for an exercise.
/// Stored as integer in exercise_patterns.interaction_mode.
/// Never reorder or insert — append only.
/// </summary>
public enum InteractionMode
{
    ReadOnly         = 0,  // Micro-lesson or reflection: student reads/listens, no submission
    FreeTextEntry    = 1,  // Open text box (email reply, writing scenario, spoken response)
    GapFill          = 2,  // Inline blanks in a passage — click word bank or type
    MultipleChoice   = 3,  // Select one from A/B/C/D options
    MatchingPairs    = 4,  // Click-to-pair two columns (phrase ↔ meaning)
    SentenceBuilder  = 5,  // Drag scrambled words into correct order
    ErrorCorrection  = 6,  // Highlight errors in text, type corrections
    ChatReply        = 7,  // Teams-style chat bubble UI, typed reply with word counter
    AudioAndFreeText = 8,  // Play audio clip first, then free-text answer area
    AudioAndGapFill  = 9,  // Play audio, fill gaps in a gapped transcript
    EmailReply       = 10, // Structured subject + body fields for workplace email replies
    AudioResponse         = 11, // Record a spoken response via microphone, submitted as audio
    MultipleChoiceMulti   = 12, // Select multiple correct answers from A/B/C/D options
    ReadingFillInBlanks   = 13, // Reading passage with per-gap dropdown option selection
}
