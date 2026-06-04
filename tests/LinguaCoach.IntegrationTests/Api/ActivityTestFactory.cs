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

        foreach (var key in new[] { "activity_generate_writing", "activity_evaluate_writing", "learning_path_generate" })
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
              "pathTitle": "Workplace English for Document Controller — B1",
              "modules": [
                { "order": 1, "title": "Professional email writing", "description": "Practice formal workplace emails." },
                { "order": 2, "title": "Meeting communication", "description": "Build confidence in meetings." },
                { "order": 3, "title": "Document control language", "description": "Practice transmittals and approvals." },
                { "order": 4, "title": "Formal requests", "description": "Learn to write and respond to formal requests." },
                { "order": 5, "title": "Workplace relationships", "description": "Everyday professional communication." }
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

    public AiProviderSelection ResolveWritingFeedbackProvider()
        => new(_provider, _provider.ProviderName, "fake-model");
}
