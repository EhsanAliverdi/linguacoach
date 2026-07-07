using System.Text.Json;
using System.Text.RegularExpressions;
using LinguaCoach.Application.FormIo;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

// Backfills the placement item bank that was previously hardcoded in
// PlacementAssessmentService.cs into admin-editable rows. Idempotent: re-runs are safe — a
// (skill, CEFR level) pair that already has any rows is assumed fully seeded and is skipped for
// *insertion* (never duplicated on the next app restart, never clobbers an admin's later edit to
// an item's Form.io schema). Separately, any existing row within an already-seeded pair that is
// still missing FormIoSchemaJson (e.g. rows created before the Form.io-native migration ran
// against this database) is backfilled in place, matched to its corresponding SeedItem by
// position within the pair — never touching a row that already has a schema. Rows that already
// have FormIoSchemaJson/ScoringRulesJson but no AuthoringSchemaJson (seeded before the Quiz tab
// existed) are similarly backfilled by re-embedding their existing scoring rules as quiz
// annotations (FormIoQuizAnnotationCodec.Embed) — never touching a row an admin has since
// re-authored through the Quiz tab UI (which always sets AuthoringSchemaJson non-null).
//
// Form.io-native migration: every seeded item is authored with a native FormIoSchemaJson
// (single "answer" component) plus a matching backend-only ScoringRulesJson, generated
// programmatically from the same flat SeedItem data (ItemType/Prompt are seed-fixture inputs
// only now — PlacementItemDefinition itself no longer stores them).
public static class PlacementItemBankSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db)
    {
        var existing = await db.PlacementItemDefinitions.ToListAsync();
        var existingByPair = existing
            .GroupBy(i => (i.Skill, i.CefrLevel))
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.ItemOrder).ToList());

        var defaultsByPair = DefaultItems
            .Select((t, idx) => (t, order: idx + 1))
            .GroupBy(x => (x.t.Skill, x.t.CefrLevel))
            .ToDictionary(g => g.Key, g => g.ToList());

        var toAdd = new List<PlacementItemDefinition>();
        var dirty = false;

        foreach (var (pair, seedEntries) in defaultsByPair)
        {
            if (existingByPair.TryGetValue(pair, out var existingRows))
            {
                // Pair already seeded — never insert new rows for it. Only repair rows still
                // missing a schema (pre-Form.io-migration backfill) or missing a quiz-annotated
                // authoring schema (pre-Quiz-tab backfill), matched by position.
                var matchCount = Math.Min(existingRows.Count, seedEntries.Count);
                for (var i = 0; i < matchCount; i++)
                {
                    var row = existingRows[i];
                    var t = seedEntries[i].t;

                    if (string.IsNullOrWhiteSpace(row.FormIoSchemaJson))
                    {
                        var (schema, rules) = BuildFormIoAuthoring(t);
                        row.SetFormIoAuthoring(schema, rules);
                        dirty = true;
                    }

                    if (string.IsNullOrWhiteSpace(row.AuthoringSchemaJson) && !string.IsNullOrWhiteSpace(row.FormIoSchemaJson))
                    {
                        row.SetAuthoringSchema(FormIoQuizAnnotationCodec.Embed(row.FormIoSchemaJson!, row.ScoringRulesJson));
                        dirty = true;
                    }
                }
                continue;
            }

            foreach (var (t, order) in seedEntries)
            {
                var (formIoSchemaJson, scoringRulesJson) = BuildFormIoAuthoring(t);

                var newItem = new PlacementItemDefinition(t.Skill, t.CefrLevel, order);
                newItem.SetFormIoAuthoring(formIoSchemaJson, scoringRulesJson);
                newItem.SetAuthoringSchema(FormIoQuizAnnotationCodec.Embed(formIoSchemaJson, scoringRulesJson));
                toAdd.Add(newItem);
            }
        }

        if (toAdd.Count > 0) db.PlacementItemDefinitions.AddRange(toAdd);
        if (toAdd.Count > 0 || dirty) await db.SaveChangesAsync();
    }

    private static readonly Regex ChoicePattern = new(@"\(([A-Z])\)\s*([^(]+?)(?=\s*\([A-Z]\)|$)", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Builds the native Form.io schema + matching scoring rules for one seed item.
    /// multiple_choice items become a "radio" component scored as single_choice; gap_fill items
    /// become a "textfield" component scored as text_normalized (case-insensitive trim compare,
    /// matching the legacy PlacementScoringService's comparison semantics). Reading items with a
    /// Passage get a read-only "content" component rendered before the question, so the passage is
    /// always visible while answering (never crammed into the question's own label). Listening
    /// items carry their spoken content only in ListeningScript (backend-only, synthesized to audio
    /// by AdaptivePlacementAudioService) — the visible question/label never repeats the transcript,
    /// otherwise the "listening" skill would be answerable by reading alone.</summary>
    private static (string FormIoSchemaJson, string ScoringRulesJson) BuildFormIoAuthoring(SeedItem t)
    {
        object questionComponent;
        object rule;

        if (t.ItemType == "multiple_choice")
        {
            var choiceIdx = t.Prompt.IndexOf("(A)", StringComparison.OrdinalIgnoreCase);
            var label = choiceIdx > 0 ? t.Prompt[..choiceIdx].Trim() : t.Prompt.Trim();
            var choices = ChoicePattern.Matches(t.Prompt)
                .Select(m => new { key = m.Groups[1].Value.ToUpperInvariant(), label = m.Groups[2].Value.Trim() })
                .ToList();

            questionComponent = new
            {
                type = "radio",
                key = "answer",
                label,
                values = choices.Select(c => new { label = c.label, value = c.key }).ToArray(),
            };
            rule = new { kind = "single_choice", correctAnswer = t.CorrectAnswer, points = 1.0 };
        }
        else
        {
            questionComponent = new
            {
                type = "textfield",
                key = "answer",
                label = t.Prompt,
            };
            rule = new { kind = "text_normalized", correctAnswer = t.CorrectAnswer, points = 1.0 };
        }

        var components = new List<object>();
        if (!string.IsNullOrWhiteSpace(t.Passage))
        {
            components.Add(new
            {
                type = "content",
                key = "reading_passage",
                input = false,
                html = $"<p>{System.Net.WebUtility.HtmlEncode(t.Passage)}</p>",
            });
        }
        components.Add(questionComponent);

        var schema = new { components = components.ToArray() };
        var scoringRules = new Dictionary<string, object?>
        {
            ["components"] = new Dictionary<string, object> { ["answer"] = rule },
            ["listeningAudioScript"] = t.Skill == "listening" ? t.ListeningScript : null,
        };

        return (JsonSerializer.Serialize(schema, JsonOptions), JsonSerializer.Serialize(scoringRules, JsonOptions));
    }

    private sealed record SeedItem(
        string Skill, string CefrLevel, string ItemType, string Prompt, string CorrectAnswer,
        string? Passage = null, string? ListeningScript = null);

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

        // Reading — the passage is shown as a read-only block before the question (never crammed
        // into the question's own label). B2 items reference "the passage"/"the text" explicitly,
        // so they carry a real short passage rather than an implied one.
        new("reading", "A1", "multiple_choice", "What did the cat do? (A) stand (B) sit (C) run", "B", Passage: "The cat sat on the mat."),
        new("reading", "A1", "multiple_choice", "What fruit does John like? (A) oranges (B) apples (C) bananas", "B", Passage: "John likes apples."),
        new("reading", "A1", "gap_fill", "Complete: 'The dog is ___.' (big/run/eat)", "big"),

        new("reading", "A2", "multiple_choice", "Where does she work? (A) school (B) hospital (C) office", "B", Passage: "She works in a hospital as a nurse."),
        new("reading", "A2", "multiple_choice", "When does it close? (A) 9am (B) 12pm (C) 6pm", "C", Passage: "The store opens at 9am and closes at 6pm."),
        new("reading", "A2", "gap_fill", "Complete: 'He ___ a book every week.' (reads/eat/drives)", "reads"),

        new("reading", "B1", "multiple_choice", "What happened? (A) cancelled (B) successful (C) postponed", "B", Passage: "Despite the rain, the event was a success."),
        new("reading", "B1", "multiple_choice", "What does the study show? (A) diet affects sleep (B) exercise improves mood (C) rest reduces stress", "B", Passage: "The study concluded that exercise improves mood."),
        new("reading", "B1", "gap_fill", "Complete: 'The report highlights several ___.' (concerns/concerned/concern)", "concerns"),

        new("reading", "B2", "multiple_choice",
            "The passage implies that the author: (A) supports the policy (B) questions its effectiveness (C) ignores the data", "B",
            Passage: "The new policy was introduced amid much fanfare, promising to reduce costs within a year. Yet six months in, the data tells a different story: costs have barely moved, and several departments report increased administrative burden. Officials continue to praise the policy publicly, but internal memos suggest growing unease about its actual impact."),
        new("reading", "B2", "multiple_choice",
            "The word 'mitigate' in the passage most closely means: (A) worsen (B) reduce (C) ignore", "B",
            Passage: "Engineers proposed several measures to mitigate the flooding risk, including raised embankments and improved drainage. While these steps would not eliminate the danger entirely, officials argued they would meaningfully reduce the likelihood of future damage."),
        new("reading", "B2", "gap_fill",
            "Complete the inference: 'The author suggests that the problem is ___.' (systemic/individual/minor)", "systemic",
            Passage: "After the third factory recall this year, executives blamed a single supplier for the defects. But industry analysts point out that similar failures have occurred across multiple suppliers and product lines, suggesting the root cause lies deeper than any one vendor."),

        // Listening — the visible question never repeats the spoken transcript verbatim (that
        // would make the "listening" skill answerable by reading alone). ListeningScript is
        // backend-only and synthesized to audio by AdaptivePlacementAudioService.
        new("listening", "A1", "multiple_choice", "Where do you turn? (A) right (B) left (C) straight", "B", ListeningScript: "Turn left at the traffic lights."),
        new("listening", "A1", "multiple_choice", "How much is it? (A) 3 euros (B) 15 euros (C) 5 euros", "C", ListeningScript: "The price is five euros."),
        new("listening", "A1", "gap_fill", "Complete what you hear: 'My name is ___.' (Maria/Monday/Morning)", "Maria", ListeningScript: "My name is Maria."),

        new("listening", "A2", "multiple_choice", "When is the meeting? (A) Thursday 3pm (B) Friday 3pm (C) Friday 5pm", "B", ListeningScript: "The meeting is on Friday at 3pm."),
        new("listening", "A2", "multiple_choice", "When will it rain? (A) morning (B) afternoon (C) evening", "B", ListeningScript: "Expect rain in the afternoon."),
        new("listening", "A2", "gap_fill", "Complete what you hear: 'Please ___ at reception.' (arrive/register/leave)", "register", ListeningScript: "Please register at reception."),

        new("listening", "B1", "multiple_choice", "What is the caller's main concern? (A) price (B) quality (C) speed", "C",
            ListeningScript: "I've been waiting for over an hour and no one has come to help me. This is unacceptable — everything here is far too slow."),
        new("listening", "B1", "multiple_choice", "What does the speaker suggest? (A) decide quickly (B) consider both sides (C) avoid the decision", "B",
            ListeningScript: "We need to consider both sides before deciding."),
        new("listening", "B1", "gap_fill", "Complete what you hear: 'The deadline has been ___.' (extended/shortened/cancelled)", "extended", ListeningScript: "The deadline has been extended."),

        new("listening", "B2", "multiple_choice", "The second speaker's tone is: (A) dismissive (B) conciliatory (C) aggressive", "B",
            ListeningScript: "I understand your concerns, and I think we can find a solution that works for everyone if we keep talking this through calmly."),
        new("listening", "B2", "multiple_choice", "The speaker implies the proposal: (A) is fully funded (B) needs revision (C) has been rejected", "B",
            ListeningScript: "The proposal has some promising ideas, but honestly, it still needs quite a bit of work before it's ready to move forward."),
        new("listening", "B2", "gap_fill", "Complete what you hear: 'The analysis was ___.' (inconclusive/concluded/conclusive)", "inconclusive", ListeningScript: "The analysis was inconclusive."),

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
