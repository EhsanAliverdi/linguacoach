using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

public static class WritingScenarioSeeder
{
    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var hasAny = await db.WritingScenarios.AnyAsync(ct);
        if (hasAny) return;

        var scenarios = new[]
        {
            new WritingScenario(
                title: "Follow up on a pending document approval",
                situation: "You submitted an important document to your project manager 5 working days ago, but it has not been approved yet. The approval is blocking the next phase of the project.",
                learningGoal: "Learn how to follow up professionally without sounding pushy. Use indirect language and a polite closing.",
                targetPhrasesJson: """["I wanted to follow up on","I would appreciate it if you could","As previously discussed","Please let me know","I look forward to your response"]""",
                targetVocabularyJson: """["pending","approval","submitted","critical","phase"]""",
                exampleText: "Dear Mr. Ahmadi,\n\nI hope you are well. I wanted to follow up on the design document I submitted on 26 May. As previously discussed, approval is needed before we can begin the next phase.\n\nI would appreciate it if you could review it at your earliest convenience. Please let me know if you need any additional information.\n\nI look forward to your response.\n\nBest regards,\nSara",
                commonMistakeToAvoid: "Avoid writing 'Why haven't you approved it yet?' — this sounds rude. Use 'I wanted to follow up on' instead.",
                difficulty: "B1"),

            new WritingScenario(
                title: "Ask for missing information politely",
                situation: "A colleague sent you an incomplete report. You cannot complete your task without the missing data. You need to ask for it without making them feel criticised.",
                learningGoal: "Learn how to request missing information diplomatically. Use softening phrases and explain why you need it.",
                targetPhrasesJson: """["I was wondering if you could","Could you please send","I noticed that","In order to proceed","Would it be possible to"]""",
                targetVocabularyJson: """["complete","attached","missing","clarification","deadline"]""",
                exampleText: "Dear Reza,\n\nThank you for sending the report. I noticed that the cost breakdown section was not included. In order to proceed with the budget approval, I need those figures.\n\nWould it be possible to send the missing section by Thursday? Please let me know if you need more time.\n\nBest regards,\nAli",
                commonMistakeToAvoid: "Avoid 'You forgot to attach the data' — it sounds accusatory. Say 'I noticed that the section was not included' instead.",
                difficulty: "A2"),

            new WritingScenario(
                title: "Explain a delay professionally",
                situation: "Your task is running two days late due to a supplier issue. Your manager expects the work to be finished by today. You need to explain the delay and give a new deadline.",
                learningGoal: "Learn how to explain a delay without making excuses. Acknowledge the impact, give a clear new date, and show accountability.",
                targetPhrasesJson: """["I regret to inform you","due to","I apologise for any inconvenience","The revised deadline","I will ensure"]""",
                targetVocabularyJson: """["delay","supplier","revised","inconvenience","committed"]""",
                exampleText: "Dear Ms. Johnson,\n\nI regret to inform you that the material schedule is delayed by two days due to a supplier issue outside our control.\n\nI apologise for any inconvenience this may cause. The revised deadline for delivery is 6 June. I will ensure all remaining tasks are completed on time.\n\nBest regards,\nKamran",
                commonMistakeToAvoid: "Avoid vague language like 'It will be ready soon.' Always give a specific revised date.",
                difficulty: "B1"),

            new WritingScenario(
                title: "Send a revised document",
                situation: "Your manager asked you to revise a technical report after the first review. You have made the changes and now need to send the updated version with a clear explanation of what changed.",
                learningGoal: "Learn how to summarise changes clearly and professionally when submitting a revised document.",
                targetPhrasesJson: """["Please find attached","As requested","I have revised","The main changes include","Please do not hesitate to contact me"]""",
                targetVocabularyJson: """["revised","attached","incorporated","feedback","updated"]""",
                exampleText: "Dear Mr. Lee,\n\nPlease find attached the revised version of the concrete mix report.\n\nAs requested, I have incorporated your feedback. The main changes include an updated materials table on page 3 and a revised cost estimate in section 4.\n\nPlease do not hesitate to contact me if you require any further changes.\n\nBest regards,\nNadia",
                commonMistakeToAvoid: "Do not just say 'I fixed it.' Briefly list the specific changes so the reviewer knows what to look at.",
                difficulty: "A2"),

            new WritingScenario(
                title: "Request clarification on instructions",
                situation: "Your manager gave you a task with unclear instructions. You are unsure whether to use Metric or Imperial units in the specification. You need to clarify before you start work.",
                learningGoal: "Learn how to ask for clarification without appearing incompetent. Use polite, indirect questions.",
                targetPhrasesJson: """["I wanted to clarify","Could you confirm","Before I proceed","I want to make sure","Would you prefer"]""",
                targetVocabularyJson: """["clarification","specification","confirm","proceed","metric"]""",
                exampleText: "Dear Mr. Hassan,\n\nI wanted to clarify one point before I begin the material specification. Could you confirm whether the measurements should be in Metric or Imperial units?\n\nI want to make sure the document meets the project requirements. Before I proceed, I will wait for your confirmation.\n\nThank you.\n\nBest regards,\nFarhan",
                commonMistakeToAvoid: "Avoid 'Your instructions were not clear.' Frame it as your own need for clarification, not a criticism of the sender.",
                difficulty: "A2"),

            new WritingScenario(
                title: "Update your manager on task progress",
                situation: "Your manager asked for a weekly progress update on the site inspection report. The report is 70% complete. You need to give a brief, structured status update.",
                learningGoal: "Learn how to write a clear and concise progress update. Include percentage complete, what is done, what remains, and the expected finish date.",
                targetPhrasesJson: """["I am writing to update you","The report is currently","The remaining sections","I expect to complete","Please let me know if you need"]""",
                targetVocabularyJson: """["progress","inspection","complete","remaining","schedule"]""",
                exampleText: "Dear Ms. Karimi,\n\nI am writing to update you on the site inspection report. The report is currently 70% complete. Sections 1 to 4 have been finalised.\n\nThe remaining sections cover electrical and plumbing systems. I expect to complete the full report by Friday, 7 June.\n\nPlease let me know if you need any additional details.\n\nBest regards,\nOmid",
                commonMistakeToAvoid: "Avoid long paragraphs. A progress update should be brief, with a clear current status and a specific completion date.",
                difficulty: "B1"),

            new WritingScenario(
                title: "Apologise for a mistake professionally",
                situation: "You sent the wrong version of a drawing to a client. The client noticed the error. You need to apologise, explain briefly what happened, and send the correct file.",
                learningGoal: "Learn how to apologise at work. Acknowledge the mistake, take responsibility, and show what you have done to fix it.",
                targetPhrasesJson: """["I sincerely apologise","I take full responsibility","Please find attached the correct","This was an oversight","I will ensure this does not happen again"]""",
                targetVocabularyJson: """["error","oversight","correct","sincerely","inconvenience"]""",
                exampleText: "Dear Mr. Chang,\n\nI sincerely apologise for sending the incorrect drawing earlier today. This was an oversight on my part, and I take full responsibility for the error.\n\nPlease find attached the correct version, Revision C. I will ensure this does not happen again.\n\nThank you for your understanding.\n\nBest regards,\nLeila",
                commonMistakeToAvoid: "Do not give a long explanation for why the mistake happened. Acknowledge it briefly, then focus on the fix.",
                difficulty: "B1"),

            new WritingScenario(
                title: "Confirm receipt of documents",
                situation: "A subcontractor sent you a package of signed contracts. You need to confirm you received them and let them know when you will process them.",
                learningGoal: "Learn how to write a professional acknowledgement email. Confirm what you received, the date, and the next step.",
                targetPhrasesJson: """["I am writing to confirm","We have received","As agreed","I will review","You will hear from us"]""",
                targetVocabularyJson: """["received","confirm","documents","processing","acknowledge"]""",
                exampleText: "Dear Ms. Taheri,\n\nI am writing to confirm that we have received the signed contracts you sent on 2 June 2026.\n\nAs agreed, I will review the documents and pass them to our legal team for processing. You will hear from us by 9 June with any questions or next steps.\n\nThank you for sending these promptly.\n\nBest regards,\nDavid",
                commonMistakeToAvoid: "Do not just say 'Got it, thanks.' A professional acknowledgement should name what was received and give a clear next step or timeline.",
                difficulty: "A2"),
        };

        db.WritingScenarios.AddRange(scenarios);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} writing scenarios.", scenarios.Length);
    }
}
