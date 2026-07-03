using System.Text.RegularExpressions;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

// Backfills the placement item bank that was previously hardcoded in
// PlacementAssessmentService.cs into admin-editable rows. Idempotent: re-runs are safe —
// each hardcoded item's Prompt text is its stable identity (also enforced as a unique DB
// index), so items already present (including any an admin has since edited) are skipped.
// Items an admin adds/removes via /admin/placement-items are never touched by this seeder.
public static class PlacementItemBankSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db)
    {
        var existing = await db.PlacementItemDefinitions.ToListAsync();
        var existingByPrompt = existing.ToDictionary(i => i.Prompt, StringComparer.Ordinal);

        var order = 0;
        var toAdd = new List<PlacementItemDefinition>();
        var dirty = false;
        foreach (var t in DefaultItems)
        {
            order++;
            var audioScript = t.Skill == "listening" ? DeriveListeningAudioScript(t.Prompt, t.CorrectAnswer) : null;

            if (existingByPrompt.TryGetValue(t.Prompt, out var existingItem))
            {
                // Phase 20I-5: backfill ListeningAudioScript onto rows the seeder already created
                // before this field existed (Phase 20I-4 deployed with it always null). Only
                // touches rows whose CorrectAnswer still matches the seed default — an admin who
                // has since edited the item's answer keeps their content untouched.
                if (audioScript is not null
                    && existingItem.ListeningAudioScript is null
                    && existingItem.CorrectAnswer == t.CorrectAnswer)
                {
                    existingItem.Update(
                        existingItem.Skill, existingItem.CefrLevel, existingItem.ItemType,
                        existingItem.Prompt, existingItem.CorrectAnswer, existingItem.ItemOrder,
                        existingItem.IsEnabled, existingItem.ReadingPassage, audioScript);
                    dirty = true;
                }
                continue;
            }

            toAdd.Add(new PlacementItemDefinition(
                t.Skill, t.CefrLevel, t.ItemType, t.Prompt, t.CorrectAnswer, order,
                listeningAudioScript: audioScript));
        }

        if (toAdd.Count > 0) db.PlacementItemDefinitions.AddRange(toAdd);
        if (toAdd.Count > 0 || dirty) await db.SaveChangesAsync();
    }

    private static readonly Regex QuotedTextPattern = new("'([^']+)'", RegexOptions.Compiled);

    /// <summary>
    /// Best-effort derivation of a TTS-ready script from a listening prompt's quoted "You hear: '...'"
    /// text (Phase 20I-5). Substitutes a "___" gap with the correct answer so the spoken sentence
    /// reads naturally. Returns null when the prompt has no quoted text to extract (a few listening
    /// items describe a scenario in prose rather than quoting a line, e.g. "You hear a complaint about
    /// slow service...") — those items keep showing as text-only, same as before this phase.
    /// </summary>
    private static string? DeriveListeningAudioScript(string prompt, string correctAnswer)
    {
        var match = QuotedTextPattern.Match(prompt);
        if (!match.Success) return null;

        var script = match.Groups[1].Value;
        return script.Contains("___") ? script.Replace("___", correctAnswer) : script;
    }

    private sealed record SeedItem(string Skill, string CefrLevel, string ItemType, string Prompt, string CorrectAnswer);

    // Verbatim backfill of the 72 items previously hardcoded as PlacementAssessmentService.ItemBank.
    private static readonly List<SeedItem> DefaultItems =
    [
        // Grammar
        new("grammar", "A1", "multiple_choice", "Which is correct? 'I ___ happy.' (A) am (B) is (C) are", "A"),
        new("grammar", "A1", "multiple_choice", "Choose: 'She ___ a teacher.' (A) am (B) is (C) are", "B"),
        new("grammar", "A1", "gap_fill", "Complete: 'They ___ students.' (am/is/are)", "are"),

        new("grammar", "A2", "multiple_choice", "Which is past tense? 'Yesterday I ___ to school.' (A) go (B) went (C) gone", "B"),
        new("grammar", "A2", "multiple_choice", "Choose: 'We have ___ the report.' (A) wrote (B) write (C) written", "C"),
        new("grammar", "A2", "gap_fill", "Complete: 'She ___ working here since 2020.' (has/have/had)", "has"),

        new("grammar", "B1", "multiple_choice", "Select: 'If I ___ more time, I would study harder.' (A) have (B) had (C) has", "B"),
        new("grammar", "B1", "multiple_choice", "Choose: 'The report ___ by the manager tomorrow.' (A) will review (B) will be reviewed (C) reviewed", "B"),
        new("grammar", "B1", "gap_fill", "Complete: 'She suggested ___ the meeting.' (postpone/postponing/to postpone)", "postponing"),

        new("grammar", "B2", "multiple_choice", "Select: 'Had she known, she ___ earlier.' (A) would arrive (B) would have arrived (C) had arrived", "B"),
        new("grammar", "B2", "multiple_choice", "Choose the correct form: 'The data ___ been analysed.' (A) have (B) has (C) had", "A"),
        new("grammar", "B2", "gap_fill", "Complete: 'It is essential that he ___ on time.' (be/is/was)", "be"),

        // Vocabulary
        new("vocabulary", "A1", "multiple_choice", "What does 'big' mean? (A) large (B) small (C) fast", "A"),
        new("vocabulary", "A1", "multiple_choice", "Which word means 'to eat in the morning'? (A) breakfast (B) lunch (C) dinner", "A"),
        new("vocabulary", "A1", "gap_fill", "Fill in: 'I drink ___ every morning.' (water/sky/chair)", "water"),

        new("vocabulary", "A2", "multiple_choice", "What is a synonym for 'happy'? (A) sad (B) cheerful (C) angry", "B"),
        new("vocabulary", "A2", "multiple_choice", "Choose the correct word: 'The meeting was ___.' (A) postponed (B) posting (C) post", "A"),
        new("vocabulary", "A2", "gap_fill", "Complete: 'She gave a ___ speech at the event.' (powerful/power/powering)", "powerful"),

        new("vocabulary", "B1", "multiple_choice", "What does 'ambiguous' mean? (A) unclear (B) obvious (C) simple", "A"),
        new("vocabulary", "B1", "multiple_choice", "Choose the best word: 'The contract was ___.' (A) terminated (B) terminating (C) terminate", "A"),
        new("vocabulary", "B1", "gap_fill", "Fill in: 'His answer was ___; nobody understood it.' (vague/clear/exact)", "vague"),

        new("vocabulary", "B2", "multiple_choice", "What does 'ubiquitous' mean? (A) rare (B) everywhere (C) ancient", "B"),
        new("vocabulary", "B2", "multiple_choice", "Select the most formal: (A) get (B) obtain (C) grab", "B"),
        new("vocabulary", "B2", "gap_fill", "Complete: 'The new policy was met with widespread ___.' (resistance/resist/resistant)", "resistance"),

        // Reading
        new("reading", "A1", "multiple_choice", "Read: 'The cat sat on the mat.' What did the cat do? (A) stand (B) sit (C) run", "B"),
        new("reading", "A1", "multiple_choice", "Read: 'John likes apples.' What fruit does John like? (A) oranges (B) apples (C) bananas", "B"),
        new("reading", "A1", "gap_fill", "Read: 'The dog is ___.' Complete with: (big/run/eat)", "big"),

        new("reading", "A2", "multiple_choice", "Read: 'She works in a hospital as a nurse.' Where does she work? (A) school (B) hospital (C) office", "B"),
        new("reading", "A2", "multiple_choice", "Read: 'The store opens at 9am and closes at 6pm.' When does it close? (A) 9am (B) 12pm (C) 6pm", "C"),
        new("reading", "A2", "gap_fill", "Read: 'He ___ a book every week.' (reads/eat/drives)", "reads"),

        new("reading", "B1", "multiple_choice", "Read: 'Despite the rain, the event was a success.' What happened? (A) cancelled (B) successful (C) postponed", "B"),
        new("reading", "B1", "multiple_choice", "Read: 'The study concluded that exercise improves mood.' What does the study show? (A) diet affects sleep (B) exercise improves mood (C) rest reduces stress", "B"),
        new("reading", "B1", "gap_fill", "Read: 'The report highlights several ___.' (concerns/concerned/concern)", "concerns"),

        new("reading", "B2", "multiple_choice", "The passage implies that the author: (A) supports the policy (B) questions its effectiveness (C) ignores the data", "B"),
        new("reading", "B2", "multiple_choice", "The word 'mitigate' in the text most closely means: (A) worsen (B) reduce (C) ignore", "B"),
        new("reading", "B2", "gap_fill", "Complete the inference: 'The author suggests that the problem is ___.' (systemic/individual/minor)", "systemic"),

        // Listening (simulated with text descriptions)
        new("listening", "A1", "multiple_choice", "You hear: 'Turn left at the traffic lights.' Where do you turn? (A) right (B) left (C) straight", "B"),
        new("listening", "A1", "multiple_choice", "You hear: 'The price is five euros.' How much is it? (A) 3 euros (B) 15 euros (C) 5 euros", "C"),
        new("listening", "A1", "gap_fill", "You hear: 'My name is ___.' (Maria/Monday/Morning)", "Maria"),

        new("listening", "A2", "multiple_choice", "You hear: 'The meeting is on Friday at 3pm.' When is the meeting? (A) Thursday 3pm (B) Friday 3pm (C) Friday 5pm", "B"),
        new("listening", "A2", "multiple_choice", "You hear a weather report: 'Expect rain in the afternoon.' When will it rain? (A) morning (B) afternoon (C) evening", "B"),
        new("listening", "A2", "gap_fill", "You hear: 'Please ___ at reception.' (arrive/register/leave)", "register"),

        new("listening", "B1", "multiple_choice", "You hear a complaint about slow service. What is the caller's main concern? (A) price (B) quality (C) speed", "C"),
        new("listening", "B1", "multiple_choice", "You hear: 'We need to consider both sides before deciding.' What does the speaker suggest? (A) decide quickly (B) consider both sides (C) avoid the decision", "B"),
        new("listening", "B1", "gap_fill", "You hear: 'The deadline has been ___.' (extended/shortened/cancelled)", "extended"),

        new("listening", "B2", "multiple_choice", "You hear a debate. The second speaker's tone is: (A) dismissive (B) conciliatory (C) aggressive", "B"),
        new("listening", "B2", "multiple_choice", "The speaker implies the proposal: (A) is fully funded (B) needs revision (C) has been rejected", "B"),
        new("listening", "B2", "gap_fill", "You hear: 'The analysis was ___.' (inconclusive/concluded/conclusive)", "inconclusive"),

        // Writing (self-assessment proxy - deterministic)
        new("writing", "A1", "multiple_choice", "Which sentence is correct? (A) 'I writed a letter.' (B) 'I wrote a letter.' (C) 'I writing a letter.'", "B"),
        new("writing", "A1", "multiple_choice", "Which is correctly punctuated? (A) 'hello how are you' (B) 'Hello, how are you?' (C) 'Hello how are you!'", "B"),
        new("writing", "A1", "gap_fill", "Choose the correct sentence ending: 'She ___ to school every day.' (go/goes/going)", "goes"),

        new("writing", "A2", "multiple_choice", "Which opening is best for a formal email? (A) 'Hey!' (B) 'Dear Sir/Madam,' (C) 'Yo!'", "B"),
        new("writing", "A2", "multiple_choice", "Which is a complete sentence? (A) 'Running fast.' (B) 'She runs fast.' (C) 'Fast running.'", "B"),
        new("writing", "A2", "gap_fill", "Complete: '___ you for your email.' (Thank/Thanks/Thanking)", "Thank"),

        new("writing", "B1", "multiple_choice", "Which transition best shows contrast? (A) Furthermore (B) However (C) Therefore", "B"),
        new("writing", "B1", "multiple_choice", "Which is the most concise? (A) 'Due to the fact that' (B) 'Because' (C) 'Owing to the reason that'", "B"),
        new("writing", "B1", "gap_fill", "Complete the formal closing: '___ regards,' (Best/Good/Fine)", "Best"),

        new("writing", "B2", "multiple_choice", "Which best hedges a claim? (A) 'It is certain that' (B) 'Evidence suggests that' (C) 'Everyone knows that'", "B"),
        new("writing", "B2", "multiple_choice", "Which shows strongest cohesion? (A) 'And also too' (B) 'Moreover' (C) 'Plus also'", "B"),
        new("writing", "B2", "gap_fill", "Complete: 'The findings ___ that further research is needed.' (indicate/indicates/indicating)", "indicate"),

        // Speaking (self-assessment proxy)
        new("speaking", "A1", "multiple_choice", "How would you greet someone in the morning? (A) 'Good morning!' (B) 'Good night!' (C) 'Goodbye!'", "A"),
        new("speaking", "A1", "multiple_choice", "How do you ask for a price? (A) 'How much is it?' (B) 'Where is it?' (C) 'When is it?'", "A"),
        new("speaking", "A1", "gap_fill", "Complete: '___ me to your manager, please.' (Take/Introduce/Tell)", "Introduce"),

        new("speaking", "A2", "multiple_choice", "How do you politely decline an invitation? (A) 'No way!' (B) 'I'm afraid I can't make it.' (C) 'That's boring.'", "B"),
        new("speaking", "A2", "multiple_choice", "Which is more polite? (A) 'Give me water.' (B) 'Could I have some water, please?' (C) 'Water now.'", "B"),
        new("speaking", "A2", "gap_fill", "Complete: 'I ___ if you could help me.' (wonder/wondering/wondered)", "wonder"),

        new("speaking", "B1", "multiple_choice", "How do you interrupt politely in a meeting? (A) 'Stop talking!' (B) 'Sorry to interrupt, but...' (C) 'Be quiet!'", "B"),
        new("speaking", "B1", "multiple_choice", "Which phrase shows you're listening actively? (A) 'Whatever.' (B) 'I see what you mean.' (C) 'That's wrong.'", "B"),
        new("speaking", "B1", "gap_fill", "Complete: 'To ___ what you said...' (clarify/summarise/confirm)", "summarise"),

        new("speaking", "B2", "multiple_choice", "Which phrase best introduces a nuanced point? (A) 'Actually, it's complicated because...' (B) 'You're wrong.' (C) 'That's simple.'", "A"),
        new("speaking", "B2", "multiple_choice", "How do you diplomatically disagree? (A) 'That's completely wrong.' (B) 'I see your point, but I would argue that...' (C) 'I disagree.'", "B"),
        new("speaking", "B2", "gap_fill", "Complete: 'To ___ the discussion, I would like to add...' (build on/ignore/dismiss)", "build on"),
    ];
}
