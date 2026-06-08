namespace LinguaCoach.Application.Placement;

/// <summary>
/// Static placement assessment content for the MVP.
/// 6 sections covering all core workplace English skills.
/// Content uses BasicWorkplace / JuniorRole domain complexity only — it assesses
/// English proficiency, not professional expertise.
/// See: docs/architecture/placement-assessment-model.md
/// </summary>
public static class PlacementContent
{
    // Canonical section keys, in order.
    public const string SelfCheckKey = "self_check";
    public const string VocabGrammarKey = "vocab_grammar";
    public const string ReadingKey = "reading";
    public const string ListeningKey = "listening";
    public const string WritingKey = "writing";
    public const string SpeakingKey = "speaking";

    public static readonly IReadOnlyList<string> SectionOrder =
    [
        SelfCheckKey, VocabGrammarKey, ReadingKey, ListeningKey, WritingKey, SpeakingKey
    ];

    public static string FirstSectionKey => SectionOrder[0];

    /// <summary>Returns the next section key after the given one, or null if it is the last section.</summary>
    public static string? NextSectionKey(string sectionKey)
    {
        var idx = IndexOf(sectionKey);
        if (idx < 0 || idx >= SectionOrder.Count - 1) return null;
        return SectionOrder[idx + 1];
    }

    public static bool IsValidSection(string sectionKey) => IndexOf(sectionKey) >= 0;

    public static bool IsLastSection(string sectionKey) => IndexOf(sectionKey) == SectionOrder.Count - 1;

    public static int IndexOf(string sectionKey) =>
        SectionOrder.ToList().FindIndex(k => string.Equals(k, sectionKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the full section definition for the given key, or null if unknown.</summary>
    public static PlacementSectionDto? GetSection(string sectionKey)
        => Sections.FirstOrDefault(s => string.Equals(s.Key, sectionKey, StringComparison.OrdinalIgnoreCase));

    public static readonly IReadOnlyList<PlacementSectionDto> Sections =
    [
        new PlacementSectionDto(
            Key: SelfCheckKey,
            Order: 1,
            Title: "Quick self-check",
            Instructions: "Tell us how confident you feel right now. There are no wrong answers — this just helps us understand your starting point.",
            SectionType: "self_check",
            Scored: false,
            Questions:
            [
                new PlacementQuestionDto("confidence_email", "How confident are you writing a short work email?", "rating", null),
                new PlacementQuestionDto("confidence_speaking", "How confident are you explaining something out loud at work?", "rating", null),
                new PlacementQuestionDto("confidence_reading", "How confident are you reading work documents and messages?", "rating", null),
                new PlacementQuestionDto("confidence_listening", "How confident are you understanding people in meetings?", "rating", null),
                new PlacementQuestionDto("main_challenge", "What is your main workplace communication challenge? (optional)", "text", null),
                new PlacementQuestionDto("self_level", "If you had to guess your English level, what would it be? (optional)", "choice",
                    ["A1", "A2", "B1", "B2", "C1", "C2", "Not sure"]),
            ],
            Passage: null,
            AudioScript: null,
            WritingPrompt: null,
            SpeakingPrompt: null),

        new PlacementSectionDto(
            Key: VocabGrammarKey,
            Order: 2,
            Title: "Vocabulary and grammar",
            Instructions: "Choose the best option for each workplace sentence.",
            SectionType: "mcq",
            Scored: true,
            Questions:
            [
                new PlacementQuestionDto("vg1", "Choose the most polite option: \"___ send me the report when you have a moment?\"", "choice",
                    ["Send", "Could you", "You must", "Why don't you"], CorrectOption: "Could you"),
                new PlacementQuestionDto("vg2", "Pick the correct phrase: \"Please find the document ___.\"", "choice",
                    ["attach", "attaching", "attached", "to attach"], CorrectOption: "attached"),
                new PlacementQuestionDto("vg3", "Which is correct? \"I ___ the meeting yesterday.\"", "choice",
                    ["attend", "attended", "attending", "have attend"], CorrectOption: "attended"),
                new PlacementQuestionDto("vg4", "Choose the best collocation: \"We need to ___ a decision soon.\"", "choice",
                    ["do", "make", "take", "have"], CorrectOption: "make"),
                new PlacementQuestionDto("vg5", "Pick the most professional reply: \"___ for the delay.\"", "choice",
                    ["Sorry", "My apologies", "Oops", "No problem"], CorrectOption: "My apologies"),
                new PlacementQuestionDto("vg6", "Which sentence is correct?", "choice",
                    [
                        "He don't have time today.",
                        "He doesn't have time today.",
                        "He not have time today.",
                        "He haven't time today."
                    ], CorrectOption: "He doesn't have time today."),
            ],
            Passage: null,
            AudioScript: null,
            WritingPrompt: null,
            SpeakingPrompt: null),

        new PlacementSectionDto(
            Key: ReadingKey,
            Order: 3,
            Title: "Reading a workplace message",
            Instructions: "Read the short message, then answer the questions.",
            SectionType: "reading",
            Scored: true,
            Questions:
            [
                new PlacementQuestionDto("rd1", "Who sent this message?", "choice",
                    ["A client", "The team lead, Sara", "The IT department", "A new colleague"], CorrectOption: "The team lead, Sara"),
                new PlacementQuestionDto("rd2", "What does Sara want the team to do before Friday?", "choice",
                    ["Book a meeting room", "Send their updated task list", "Finish the whole project", "Call the client"], CorrectOption: "Send their updated task list"),
                new PlacementQuestionDto("rd3", "What should someone do if they cannot meet the deadline?", "choice",
                    ["Ignore the message", "Tell Sara as soon as possible", "Wait until Friday", "Ask the client"], CorrectOption: "Tell Sara as soon as possible"),
            ],
            Passage:
                "Hi everyone,\n\nThank you for your hard work this week. Before Friday, please send me your updated task list so I can prepare the weekly summary for the client. " +
                "If you think you cannot finish something on time, let me know as soon as possible so we can plan together. There is no problem with delays — I just need to know early.\n\nThanks,\nSara (Team Lead)",
            AudioScript: null,
            WritingPrompt: null,
            SpeakingPrompt: null),

        new PlacementSectionDto(
            Key: ListeningKey,
            Order: 4,
            Title: "Listening to a workplace message",
            Instructions: "Listen to the short voice message (or read the script if audio is unavailable), then answer the questions.",
            SectionType: "listening",
            Scored: true,
            Questions:
            [
                new PlacementQuestionDto("ls1", "Why is the speaker calling?", "choice",
                    ["To cancel the project", "To move the meeting to a later time", "To ask for a day off", "To complain about a colleague"], CorrectOption: "To move the meeting to a later time"),
                new PlacementQuestionDto("ls2", "What time is the meeting now?", "choice",
                    ["9 am", "10 am", "2 pm", "4 pm"], CorrectOption: "2 pm"),
                new PlacementQuestionDto("ls3", "What does the speaker ask you to bring?", "choice",
                    ["Your laptop", "The updated numbers", "Coffee", "Nothing"], CorrectOption: "The updated numbers"),
            ],
            Passage: null,
            AudioScript:
                "Hi, it's Daniel. Quick update about our meeting today. Something came up this morning, so I need to move our meeting from ten to two in the afternoon. " +
                "Same room, just a later time. Could you please bring the updated numbers so we can go through them together? Thanks, see you at two.",
            WritingPrompt: null,
            SpeakingPrompt: null),

        new PlacementSectionDto(
            Key: WritingKey,
            Order: 5,
            Title: "Write a short workplace reply",
            Instructions: "Write a short, professional reply (about 80-120 words). Take your time.",
            SectionType: "writing",
            Scored: true,
            Questions:
            [
                new PlacementQuestionDto("wr1", "Your reply", "text", null),
            ],
            Passage: null,
            AudioScript: null,
            WritingPrompt:
                "A colleague sent you this message:\n\n\"Hi, could you send me the final version of the report today? The client is asking for it.\"\n\n" +
                "Unfortunately, the report is not finished yet — you need one more day. Write a short, polite reply explaining the situation and giving a new time.",
            SpeakingPrompt: null),

        new PlacementSectionDto(
            Key: SpeakingKey,
            Order: 6,
            Title: "Speak a short response",
            Instructions: "Record a 30-60 second spoken response (or type what you would say if recording is unavailable).",
            SectionType: "speaking",
            Scored: true,
            Questions:
            [
                new PlacementQuestionDto("sp1", "Your spoken response", "text", null),
            ],
            Passage: null,
            AudioScript: null,
            WritingPrompt: null,
            SpeakingPrompt:
                "Tell us about a typical task you do at work. Explain what you do, who you usually work with, and one thing you find difficult. Speak for about 30-60 seconds."),
    ];
}
