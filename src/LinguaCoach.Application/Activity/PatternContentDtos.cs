namespace LinguaCoach.Application.Activity;

// ── Typed content DTOs for the 8 MVP exercise patterns ────────────────────────
//
// Each DTO defines the exact JSON contract the AI must produce for that pattern,
// and the frontend must render. AiActivityGeneratorHandler deserialises and
// re-serialises through these types to guarantee a clean, schema-compliant payload.
//
// All properties are nullable to tolerate partial AI responses — callers apply
// safe defaults when fields are missing.
//
// Naming: matches the camelCase JSON keys the AI prompts ask for.

// ── phrase_match ───────────────────────────────────────────────────────────────

public sealed class PhraseMatchContent
{
    public string? Title { get; set; }
    public string? Instructions { get; set; }
    public List<PhraseMatchPairDto>? Pairs { get; set; }
    public string? TeachingNote { get; set; }
}

public sealed class PhraseMatchPairDto
{
    public string? Phrase { get; set; }
    public string? Meaning { get; set; }
    public string? Context { get; set; }
}

// ── gap_fill_workplace_phrase ──────────────────────────────────────────────────

public sealed class GapFillWorkplacePhraseContent
{
    public string? Title { get; set; }
    public string? Instructions { get; set; }
    public List<GapFillItemDto>? Items { get; set; }
    public string? TeachingNote { get; set; }
}

public sealed class GapFillItemDto
{
    public string? Sentence { get; set; }
    public string? BlankPosition { get; set; }
    public string? Answer { get; set; }
    public List<string>? Distractors { get; set; }
    public string? Hint { get; set; }
}

// ── listen_and_answer ─────────────────────────────────────────────────────────

public sealed class ListenAndAnswerContent
{
    public string? Title { get; set; }
    public string? Scenario { get; set; }
    public string? Instructions { get; set; }
    public string? SpeakerRole { get; set; }
    public string? ListenerRole { get; set; }
    public string? AudioScript { get; set; }
    public bool? TranscriptAvailableAfterSubmit { get; set; }
    public List<ListenAndAnswerQuestionDto>? Questions { get; set; }
    public ListenAndAnswerResponseTaskDto? ResponseTask { get; set; }
}

public sealed class ListenAndAnswerQuestionDto
{
    public string? Id { get; set; }
    public string? Question { get; set; }
    public string? ExpectedAnswer { get; set; }
    public string? Type { get; set; }
}

public sealed class ListenAndAnswerResponseTaskDto
{
    public string? Prompt { get; set; }
    public string? ExpectedFocus { get; set; }
}

// ── listen_and_gap_fill ───────────────────────────────────────────────────────

public sealed class ListenAndGapFillContent
{
    public string? Title { get; set; }
    public string? Scenario { get; set; }
    public string? Instructions { get; set; }
    public string? SpeakerRole { get; set; }
    public string? AudioScript { get; set; }
    public bool? TranscriptAvailableAfterSubmit { get; set; }
    public List<ListenAndGapFillItemDto>? Gaps { get; set; }
}

public sealed class ListenAndGapFillItemDto
{
    public string? Id { get; set; }
    public string? SentenceWithBlank { get; set; }
    public string? Answer { get; set; }
    public string? Hint { get; set; }
}

// ── email_reply ───────────────────────────────────────────────────────────────

public sealed class EmailReplyContent
{
    public string? Title { get; set; }
    public string? TaskType { get; set; }
    public string? Situation { get; set; }
    public string? Audience { get; set; }
    public string? Tone { get; set; }
    public string? ExpectedLength { get; set; }
    public string? LearningGoal { get; set; }
    public string? SkillFocus { get; set; }
    public string[]? TargetPhrases { get; set; }
    public string[]? TargetVocabulary { get; set; }
    public string? ExampleText { get; set; }
    public string? CommonMistakeToAvoid { get; set; }
    public string? InstructionInSourceLanguage { get; set; }
}

// ── teams_chat_simulation ─────────────────────────────────────────────────────

public sealed class TeamsChatSimulationContent
{
    public string? Title { get; set; }
    public string? Scenario { get; set; }
    public string? ColleagueName { get; set; }
    public string? ColleagueRole { get; set; }
    public string? StudentRole { get; set; }
    public string? LearningGoal { get; set; }
    public string? ExpectedLength { get; set; }
    public string[]? TargetPhrases { get; set; }
    public string[]? TargetVocabulary { get; set; }
    public string? ExampleReply { get; set; }
    public string? ToneGuidance { get; set; }
    public string? InstructionInSourceLanguage { get; set; }
}

// ── spoken_response_from_prompt ───────────────────────────────────────────────

public sealed class SpokenResponseContent
{
    public string? Title { get; set; }
    public string? Scenario { get; set; }
    public string? StudentRole { get; set; }
    public string? ListenerRole { get; set; }
    public string? SpeakingGoal { get; set; }
    public string? Prompt { get; set; }
    public List<string>? ExpectedPoints { get; set; }
    public List<string>? SuggestedPhrases { get; set; }
    public int? MaxDurationSeconds { get; set; }
}

// ── lesson_reflection ─────────────────────────────────────────────────────────

public sealed class LessonReflectionContent
{
    public string? Title { get; set; }
    public string? Instructions { get; set; }
    public List<string>? ReflectionPrompts { get; set; }
    public string? KeyPhrase { get; set; }
    public string? LessonSummary { get; set; }
}
