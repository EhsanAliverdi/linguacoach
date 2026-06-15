using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Base test factory for activity-related tests. Wires up a fake AI provider
/// (no real API calls), seeds WritingScenarios, and provides helpers to create
/// onboarded students. Replaces the old WritingExerciseTestFactory.
/// </summary>
public class ActivityTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddScoped<IAiProvider, FakeAiProvider>();

            var resolverDescriptors = services.Where(d => d.ServiceType == typeof(IAiProviderResolver)).ToList();
            foreach (var resolverDescriptor in resolverDescriptors)
                services.Remove(resolverDescriptor);
            services.AddScoped<IAiProviderResolver, FakeAiProviderResolver>();
        });
    }

    /// <summary>
    /// Seeds AI prompt template, writing scenarios, and curriculum word list.
    /// EnsureCreated does not run migrations so seed data is added here.
    /// </summary>
    public async Task SeedPromptTemplateAsync()
    {
        await EnsureCreatedAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        foreach (var key in new[]
        {
            "activity_generate_writing",
            "activity_evaluate_writing",
            "learning_path_generate",
            "learning_path_generate_adaptive",
            "vocabulary_extract_from_attempt",
            // Pattern-keyed generation prompts
            "activity_generate_phrase_match",
            "activity_generate_gap_fill_workplace_phrase",
            "activity_generate_email_reply",
            "activity_generate_teams_chat_simulation",
            "activity_generate_spoken_response_from_prompt",
            "activity_generate_listen_and_answer",
            "activity_generate_listen_and_gap_fill",
            "activity_generate_lesson_reflection",
            "activity_generate_speaking_roleplay",
            "activity_generate_listening",
            "activity_generate_highlight_correct_summary",
            "activity_generate_highlight_incorrect_words",
            // Pattern-keyed evaluation prompts
            "activity_evaluate_phrase_match",
            "activity_evaluate_gap_fill_workplace_phrase",
            "activity_evaluate_email_reply",
            "activity_evaluate_teams_chat_simulation",
            "activity_evaluate_spoken_response_from_prompt",
            "activity_evaluate_listen_and_answer",
            "activity_evaluate_listen_and_gap_fill",
            "activity_evaluate_lesson_reflection",
            "activity_evaluate_speaking_roleplay",
            "student_memory_update",
        })
        {
            if (!db.AiPrompts.Any(p => p.Key == key))
            {
                db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                    key, "fake-prompt-{{cefrLevel}}", maxInputTokens: 800, maxOutputTokens: 1000));
            }
        }
        await db.SaveChangesAsync();

        if (!db.AiPrompts.Any(p => p.Key == "writing.exercise.v2"))
        {
            db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                "writing.exercise.v2",
                "You are an English coach. Draft: {{userDraft}}. Return JSON: {\"overallScore\":0,\"correctedEmail\":\"\",\"feedbackInSourceLanguage\":\"\",\"grammarIssues\":[],\"vocabularyIssues\":[],\"toneIssues\":[],\"suggestedPhrases\":[],\"mistakesToTrack\":[],\"whatYouDidWell\":[],\"mainMistakes\":[],\"grammarExplanation\":\"\",\"toneExplanation\":\"\",\"vocabularyToRemember\":[],\"rewriteChallenge\":\"\",\"nextPracticeSuggestion\":\"\"}",
                maxInputTokens: 1500, maxOutputTokens: 1500));
            await db.SaveChangesAsync();
        }

        if (!db.WritingScenarios.Any())
        {
            db.WritingScenarios.Add(new LinguaCoach.Domain.Entities.WritingScenario(
                title: "Follow up on a pending document approval",
                situation: "You submitted an important document to your project manager 5 working days ago.",
                learningGoal: "Learn how to follow up professionally without sounding pushy.",
                targetPhrasesJson: "[\"I wanted to follow up on\",\"Please let me know\"]",
                targetVocabularyJson: "[\"pending\",\"approval\"]",
                exampleText: "Dear Mr. Ahmadi,\n\nI hope you are well. I wanted to follow up on the document I submitted last week.\n\nBest regards,\nSara",
                commonMistakeToAvoid: "Avoid 'Why haven't you approved it yet?' — this sounds rude.",
                difficulty: "B1"));
            await db.SaveChangesAsync();
        }

        if (!db.CurriculumWordLists.Any())
        {
            var pair = db.LanguagePairs.First();
            var career = db.CareerProfiles.First();

            var words = new[]
            {
                ("approval", "Official agreement or permission", 1),
                ("submittal", "A formal document submitted for review", 2),
                ("revision", "A corrected or updated document version", 3),
                ("pending", "Awaiting action or decision", 4),
                ("outstanding", "Not yet resolved", 5),
                ("transmittal", "A cover document recording what is sent", 6),
                ("compliance", "Meeting required standards", 7),
                ("RFI", "Request for Information", 8),
                ("specification", "A detailed technical description", 9),
                ("drawing register", "A log tracking all project drawings", 10),
            };

            foreach (var (word, def, priority) in words)
                db.CurriculumWordLists.Add(new LinguaCoach.Domain.Entities.CurriculumWordList(
                    career.Id, pair.Id, word, def, string.Empty, priority));

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Creates a student with fully completed onboarding so activity endpoints are accessible.
    /// </summary>
    public virtual async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(
        string email = "activity_student@test.linguacoach.com")
    {
        await SeedPromptTemplateAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return (tokenSvc.GenerateToken(existing.Id, existing.Email!, existing.Role), existing.Id);

        var user = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = LinguaCoach.Domain.Enums.UserRole.Student,
            EmailConfirmed = true, MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        var pair = db.LanguagePairs
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .First();
        var track = db.LearningTracks.First();
        var career = db.CareerProfiles.First();
        profile.SetLanguagePair(pair);
        profile.SetLearningTrack(track);
        profile.SetCareerProfile(career);
        profile.SetSkillFocus(LinguaCoach.Domain.Enums.SkillFocus.Writing);
        profile.SetLifecycleStage(LinguaCoach.Domain.Enums.StudentLifecycleStage.PlacementRequired);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (tokenSvc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }
}

/// <summary>Deterministic fake AI provider. Returns a valid structured JSON response including new diff/changes fields. No real API calls.</summary>
internal sealed class FakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        const string json = """
            {
              "schemaVersion": "module_stage_v1",
              "moduleGoal": "Understand a short workplace voicemail and respond appropriately.",
              "skillFocus": "listening",
              "exerciseType": "listening_comprehension",
              "learnContent": {
                "teachingTitle": "Listening for action and deadline",
                "explanation": "Listen for the main idea, the requested action, and any deadline.",
                "keyPoints": ["Focus on verbs", "Note any dates or times"],
                "examples": [{ "phrase": "by end of day", "meaning": "before today finishes", "note": "common deadline phrase" }],
                "strategy": "Listen for who, what, and when.",
                "commonMistakes": ["Missing the deadline"],
                "sourceLanguageSupport": null
              },
              "practiceContent": {
                "instructions": "Read the situation first. Then listen and answer the questions.",
                "scenario": "Your manager leaves a short voice message about a project delay.",
                "task": "Reply to confirm you received the message.",
                "exerciseData": {
                  "speakerRole": "Manager",
                  "listenerRole": "Document Controller",
                  "role": "Document Controller",
                  "partnerRole": "Manager",
                  "audioScript": "Hi, this is a test workplace voice message. Please note the meeting has been moved to Thursday.",
                  "transcriptAvailableAfterSubmit": true,
                  "prompt": "Reply to confirm you received the message.",
                  "situation": "Your manager leaves a short voice message about a project delay.",
                  "audience": "Manager",
                  "tone": "professional",
                  "questions": [
                    { "id": "q1", "question": "When was the meeting moved to?", "expectedAnswer": "Thursday", "type": "short_answer" },
                    { "id": "q2", "question": "What should you do before the meeting?", "expectedAnswer": "Check the latest delivery schedule", "type": "short_answer" }
                  ],
                  "responseTask": { "prompt": "Reply to confirm you received the message.", "expectedFocus": "acknowledgement" },
                  "pairs": [
                    { "left": "could you please", "right": "polite request" },
                    { "left": "at your earliest convenience", "right": "formal timing" }
                  ],
                  "incomingMessage": "Hi, could you send the updated drawing register by end of day? Thanks, Manager",
                  "chatHistory": [
                    { "sender": "Manager", "message": "Hi, can you confirm the delivery date?" }
                  ],
                  "partnerTurn": "Hi, can you give me a quick update on the project status?",
                  "gaps": [
                    { "id": "g1", "answer": "could you please", "options": ["could you please", "can you"] }
                  ],
                  "options": [
                    { "id": "A", "text": "The meeting was moved to Thursday." },
                    { "id": "B", "text": "The meeting was cancelled." },
                    { "id": "C", "text": "The meeting stays on Monday." },
                    { "id": "D", "text": "The meeting moved to next month." }
                  ],
                  "correctOptionId": "A",
                  "distractorExplanations": {
                    "B": "The meeting was not cancelled.",
                    "C": "The meeting did not stay on Monday.",
                    "D": "The meeting did not move to next month."
                  },
                  "displayTranscript": "Hi, this is a test workplace voice message. Please note the meeting has been moved to Friday.",
                  "tokens": [
                    { "id": "t0", "text": "Hi,", "position": 0 },
                    { "id": "t1", "text": "this", "position": 1 },
                    { "id": "t2", "text": "is", "position": 2 },
                    { "id": "t3", "text": "a", "position": 3 },
                    { "id": "t4", "text": "test", "position": 4 },
                    { "id": "t5", "text": "workplace", "position": 5 },
                    { "id": "t6", "text": "voice", "position": 6 },
                    { "id": "t7", "text": "message.", "position": 7 },
                    { "id": "t8", "text": "Please", "position": 8 },
                    { "id": "t9", "text": "note", "position": 9 },
                    { "id": "t10", "text": "the", "position": 10 },
                    { "id": "t11", "text": "meeting", "position": 11 },
                    { "id": "t12", "text": "has", "position": 12 },
                    { "id": "t13", "text": "been", "position": 13 },
                    { "id": "t14", "text": "moved", "position": 14 },
                    { "id": "t15", "text": "to", "position": 15 },
                    { "id": "t16", "text": "Friday.", "position": 16 }
                  ],
                  "incorrectTokenIds": ["t14", "t16"],
                  "corrections": { "t14": "rescheduled", "t16": "Thursday." },
                  "tokenExplanations": { "t14": "The audio says rescheduled, not moved.", "t16": "The audio says Thursday, not Friday." },
                  "items": [
                    { "vocabularyItemId": "00000000-0000-0000-0000-000000000001", "term": "could you please", "prompt": "___ send the report?", "hint": "polite request", "explanation": "Used to make polite requests." },
                    { "vocabularyItemId": "00000000-0000-0000-0000-000000000002", "term": "at your earliest convenience", "prompt": "Please respond ___.", "hint": "formal timing", "explanation": "Used to politely request a prompt response." },
                    { "vocabularyItemId": "00000000-0000-0000-0000-000000000003", "term": "please find attached", "prompt": "___ the updated document.", "hint": "email attachment phrase", "explanation": "Used to introduce an attachment in an email." }
                  ],
                  "practiceMode": "fill_blank"
                }
              },
              "feedbackPlan": {
                "evaluationCriteria": ["Main idea understood", "Requested action identified"],
                "rubric": [],
                "feedbackFocus": "Main idea and requested action",
                "successCriteria": []
              },
              "overallScore": 68,
              "coachSummary": "Good effort — your message is clear but the tone needs polishing.",
              "focusFirst": false,
              "changes": [
                {
                  "type": "replace",
                  "original": "please send",
                  "suggested": "Could you please send",
                  "reason": "Modal verbs make requests more polite in professional emails.",
                  "category": "tone",
                  "severity": "high"
                },
                {
                  "type": "replace",
                  "original": "Dear John",
                  "suggested": "Dear John,",
                  "reason": "Always place a comma after the salutation in formal emails.",
                  "category": "punctuation",
                  "severity": "medium"
                }
              ],
              "improvedVersion": "Dear John,\n\nI hope this email finds you well. Could you please send the updated document at your earliest convenience?\n\nBest regards",
              "correctedEmail": "Dear John,\n\nI hope this email finds you well. I wanted to follow up on the submittal we sent last week.\n\nBest regards",
              "feedbackInSourceLanguage": "ایمیل شما خوب بود اما می‌توانید رسمی‌تر بنویسید.",
              "grammarIssues": ["Missing comma after 'John'"],
              "vocabularyIssues": [],
              "toneIssues": ["'please send' should be 'Could you please send'"],
              "clarityIssues": [],
              "whatYouDidWell": ["Good use of formal greeting"],
              "mainMistakes": ["Missing comma after salutation"],
              "grammarExplanation": "Always place a comma after the salutation in formal emails.",
              "toneExplanation": "Your tone was professional throughout.",
              "vocabularyToRemember": ["at your earliest convenience"],
              "miniLesson": "Use modal verbs like 'could' and 'would' to make requests polite.",
              "nextImprovementStep": "Try rewriting your request sentence using 'Could you please...'",
              "rewriteChallenge": "Rewrite the opening using 'I hope this email finds you well'.",
              "nextPracticeSuggestion": "Try writing an email to explain a delay.",
              "situation": "Test situation",
              "learningGoal": "Test goal",
              "targetPhrases": ["I wanted to follow up"],
              "targetVocabulary": ["pending"],
              "exampleText": "Dear Manager,\n\nTest example.",
              "commonMistakeToAvoid": "Avoid rude phrasing.",
              "instructionInSourceLanguage": "یک ایمیل حرفه‌ای بنویسید.",
              "title": "Follow up on pending approval",
              "scenario": "You need to update your manager about a project delay.",
              "speakingGoal": "Communicate the delay professionally and propose a solution.",
              "speakingScenario": "You need to update your manager about a project delay.",
              "studentRole": "Project Coordinator",
              "listenerRole": "Manager",
              "prompt": "Record a 30-second update explaining the delay.",
              "speakingPrompt": "Record a 30-second update explaining the delay.",
              "expectedPoints": ["Acknowledge the delay", "Explain the reason", "Propose a solution"],
              "suggestedPhrases": ["I wanted to update you on", "Due to unforeseen circumstances"],
              "maxDurationSeconds": 60,
              "audioScript": "Hi, this is a test workplace voice message. Please note the meeting has been moved to Thursday.",
              "transcriptAvailableAfterSubmit": true,
              "speakerRole": "Manager",
              "questions": [
                { "id": "q1", "question": "When was the meeting moved to?", "expectedAnswer": "Thursday", "type": "short_answer" },
                { "id": "q2", "question": "What should you do before the meeting?", "expectedAnswer": "Check the latest delivery schedule", "type": "short_answer" }
              ],
              "responseTask": { "prompt": "Reply to confirm you received the message.", "expectedFocus": "acknowledgement" },
              "practiceMode": "fill_blank",
              "items": [
                { "vocabularyItemId": "00000000-0000-0000-0000-000000000001", "term": "could you please", "prompt": "___ send the report?", "hint": "polite request", "explanation": "Used to make polite requests." },
                { "vocabularyItemId": "00000000-0000-0000-0000-000000000002", "term": "at your earliest convenience", "prompt": "Please respond ___.", "hint": "formal timing", "explanation": "Used to politely request a prompt response." },
                { "vocabularyItemId": "00000000-0000-0000-0000-000000000003", "term": "please find attached", "prompt": "___ the updated document.", "hint": "email attachment phrase", "explanation": "Used to introduce an attachment in an email." }
              ],
              "pathTitle": "Workplace English for Document Controller — B1",
              "modules": [
                {
                  "order": 1,
                  "title": "Softening manager requests",
                  "description": "Practice asking managers for support without sounding too direct.",
                  "focusSkill": "softening_language",
                  "reason": "Recent attempts show direct requests need softer phrasing.",
                  "difficulty": "B1+",
                  "fingerprint": {
                    "communicationMode": "email",
                    "scenarioType": "support_request",
                    "audience": "manager",
                    "tone": "polite_professional",
                    "difficulty": "B1+",
                    "grammarFocus": "modal_verbs",
                    "vocabularyTheme": "workplace_support"
                  }
                },
                {
                  "order": 2,
                  "title": "Concise progress updates",
                  "description": "Practice short status updates with clear next steps.",
                  "focusSkill": "concise_writing",
                  "reason": "The learning memory shows long sentences and weak summarising.",
                  "difficulty": "B1+",
                  "fingerprint": {
                    "communicationMode": "email",
                    "scenarioType": "progress_update",
                    "audience": "manager",
                    "tone": "clear_professional",
                    "difficulty": "B1+",
                    "grammarFocus": "sentence_boundaries",
                    "vocabularyTheme": "project_progress"
                  }
                },
                {
                  "order": 3,
                  "title": "Summarising meeting outcomes",
                  "description": "Practice summarising decisions and action items after a meeting.",
                  "focusSkill": "summarising_information",
                  "reason": "This adds a new workplace situation while reinforcing concise structure.",
                  "difficulty": "B2",
                  "fingerprint": {
                    "communicationMode": "email",
                    "scenarioType": "meeting_summary",
                    "audience": "team",
                    "tone": "professional_neutral",
                    "difficulty": "B2",
                    "grammarFocus": "past_tense",
                    "vocabularyTheme": "actions_decisions"
                  }
                }
              ]
            }
            """;

        return Task.FromResult(new AiResponse(json, InputTokens: 450, OutputTokens: 280, CostUsd: 0.004m, ModelName: "fake-model"));
    }
}

internal sealed class MalformedFakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
        => Task.FromResult(new AiResponse("not json", 10, 10, 0, "fake-model", ProviderName));
}

internal sealed class FakeAiProviderResolver : IAiProviderResolver
{
    private readonly IAiProvider _provider;

    public FakeAiProviderResolver(IAiProvider provider)
    {
        _provider = provider;
    }

    public AiProviderPair ResolveLlm(string featureKey, string categoryKey)
        => new(new AiProviderSelection(_provider, _provider.ProviderName, "fake-model"), Fallback: null);

    public AiTtsProviderSelection ResolveTts(string featureKey, string categoryKey)
        => new("fake", "fake", "fake");
}
