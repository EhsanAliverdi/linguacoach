using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

// Seeds the default onboarding v2 flow. Idempotent: re-runs are safe.
// Flows are immutable once students have progress. A new version creates a new flow.
// Admin-configured steps are seeded disabled by default.
public static class OnboardingFlowSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db)
    {
        var existingActive = await db.OnboardingFlowDefinitions
            .AnyAsync(f => f.IsActive);

        if (existingActive) return;

        var flow = new OnboardingFlowDefinition("Default Flow", version: 1);
        flow.Activate();

        var steps = BuildDefaultSteps(flow.Id);
        // Validate no duplicate step keys before inserting.
        var keys = steps.Select(s => s.StepKey).ToList();
        var duplicates = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Any())
            throw new InvalidOperationException($"Duplicate step keys in default flow: {string.Join(", ", duplicates)}");

        foreach (var step in steps)
            flow.AddStep(step);

        db.OnboardingFlowDefinitions.Add(flow);
        await db.SaveChangesAsync();
    }

    private static List<OnboardingStepDefinition> BuildDefaultSteps(Guid flowId)
    {
        return new List<OnboardingStepDefinition>
        {
            // Step 1: Welcome
            new(
                flowDefinitionId: flowId,
                stepKey: "welcome",
                title: "Welcome to SpeakPath",
                stepType: OnboardingStepTypeV2.Welcome,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 1,
                isEnabled: true,
                description: "Let's set up your personalised English learning experience. This will only take a few minutes."
            ),

            // Step 2: Preferred name
            new(
                flowDefinitionId: flowId,
                stepKey: "preferred_name",
                title: "What should we call you?",
                stepType: OnboardingStepTypeV2.PreferredName,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 2,
                isEnabled: true,
                description: "Enter the name you'd like SpeakPath to use when talking to you.",
                validationMetadataJson: Json(new { maxLength = 100 }),
                answerMapping: OnboardingAnswerMapping.PreferredName
            ),

            // Step 3: Support language
            new(
                flowDefinitionId: flowId,
                stepKey: "support_language",
                title: "Would you like support in another language?",
                stepType: OnboardingStepTypeV2.SupportLanguage,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 3,
                isEnabled: true,
                description: "If you'd like explanations in your first language when something is unclear, select it here. This is optional — SpeakPath teaches in English.",
                optionsJson: JsonOptions(new[]
                {
                    ("none", "No, English only"),
                    ("fa", "Persian / Farsi"),
                    ("ar", "Arabic"),
                    ("zh", "Chinese (Mandarin)"),
                    ("es", "Spanish"),
                    ("pt", "Portuguese"),
                    ("hi", "Hindi"),
                    ("other", "Other")
                }),
                answerMapping: OnboardingAnswerMapping.SupportLanguage
            ),

            // Step 4: Learning goals
            new(
                flowDefinitionId: flowId,
                stepKey: "learning_goals",
                title: "What do you want to use English for?",
                stepType: OnboardingStepTypeV2.LearningGoals,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 4,
                isEnabled: true,
                description: "Select all that apply. This helps SpeakPath personalise your practice.",
                optionsJson: JsonOptions(new[]
                {
                    ("day_to_day", "Day-to-day conversations"),
                    ("work", "Work and professional communication"),
                    ("study", "Study or academic English"),
                    ("travel", "Travel"),
                    ("job_interview", "Job interviews"),
                    ("social", "Social situations"),
                    ("migration", "Migration or settlement"),
                    ("writing_confidence", "Writing confidence"),
                    ("listening_confidence", "Listening and understanding"),
                    ("pronunciation", "Pronunciation and speaking clearly")
                }),
                validationMetadataJson: Json(new { maxSelections = 5 }),
                answerMapping: OnboardingAnswerMapping.LearningGoals
            ),

            // Step 5: Focus areas
            new(
                flowDefinitionId: flowId,
                stepKey: "focus_areas",
                title: "Where would you like the most practice?",
                stepType: OnboardingStepTypeV2.FocusAreas,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 5,
                isEnabled: true,
                description: "Select your top areas. SpeakPath will prioritise these in your lessons.",
                optionsJson: JsonOptions(new[]
                {
                    ("speaking", "Speaking"),
                    ("listening", "Listening"),
                    ("writing", "Writing"),
                    ("reading", "Reading"),
                    ("vocabulary", "Vocabulary"),
                    ("grammar", "Grammar")
                }),
                validationMetadataJson: Json(new { maxSelections = 3 }),
                answerMapping: OnboardingAnswerMapping.FocusAreas
            ),

            // Step 6: Difficulty preference
            new(
                flowDefinitionId: flowId,
                stepKey: "difficulty_preference",
                title: "How challenging should your practice feel?",
                stepType: OnboardingStepTypeV2.DifficultyPreference,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 6,
                isEnabled: true,
                description: "You can change this at any time from your profile.",
                optionsJson: JsonOptions(new[]
                {
                    ("Gentle", "Gentle — I want to build confidence gradually"),
                    ("Moderate", "Moderate — a steady challenge is good for me"),
                    ("Challenging", "Challenging — push me to improve quickly")
                }),
                answerMapping: OnboardingAnswerMapping.DifficultyPreference
            ),

            // Step 7: Session duration
            new(
                flowDefinitionId: flowId,
                stepKey: "session_duration",
                title: "How much time do you want to spend in each lesson?",
                stepType: OnboardingStepTypeV2.SessionDuration,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 7,
                isEnabled: true,
                description: "We'll use this to build lessons that fit your schedule.",
                optionsJson: JsonOptions(new[]
                {
                    ("10", "10 minutes — quick daily check-in"),
                    ("15", "15 minutes — focused short practice"),
                    ("20", "20 minutes — balanced daily session"),
                    ("30", "30 minutes — deep practice session")
                }),
                answerMapping: OnboardingAnswerMapping.SessionDuration
            ),

            // Step 8: Career context — only shown to students whose learning_goals included "work"
            // (see OnboardingV2StepHandler.WorkOnlyStepKeys). AdminConfigured so it never blocks
            // completion for students who skip it.
            new(
                flowDefinitionId: flowId,
                stepKey: "career_context",
                title: "What is your job, field, or target workplace context?",
                stepType: OnboardingStepTypeV2.FreeText,
                requirementType: OnboardingStepRequirementType.AdminConfigured,
                stepOrder: 8,
                isEnabled: true,
                description: "SpeakPath uses your workplace context to create realistic, relevant practice scenarios.",
                validationMetadataJson: Json(new { maxLength = 200 }),
                answerMapping: OnboardingAnswerMapping.CareerContext
            ),

            // Step 9: Optional expanded "why" free text — no hardcoded example in any one language.
            new(
                flowDefinitionId: flowId,
                stepKey: "learning_goal_description",
                title: "Anything else you'd like to tell us?",
                stepType: OnboardingStepTypeV2.FreeText,
                requirementType: OnboardingStepRequirementType.AdminConfigured,
                stepOrder: 9,
                isEnabled: true,
                description: "Optional — describe a real situation you find difficult. You can write in your own language too.",
                validationMetadataJson: Json(new { maxLength = 1000 }),
                answerMapping: OnboardingAnswerMapping.LearningGoalDescription
            ),

            // Step 10: Work experience — only shown to students whose learning_goals included "work".
            new(
                flowDefinitionId: flowId,
                stepKey: "work_experience",
                title: "Tell us about your work experience",
                stepType: OnboardingStepTypeV2.WorkExperience,
                requirementType: OnboardingStepRequirementType.AdminConfigured,
                stepOrder: 10,
                isEnabled: true,
                description: "This helps us tailor your lessons to the right level of workplace complexity.",
                optionsJson: JsonOptions(new[]
                {
                    ("NoProfessionalExperience", "No professional experience yet"),
                    ("EntryLevelOrGraduate", "Entry level / graduate"),
                    ("Junior_0_2Years", "Junior (0-2 years)"),
                    ("MidLevel_2_5Years", "Mid-level (2-5 years)"),
                    ("Senior_5_10Years", "Senior (5-10 years)"),
                    ("LeadOrManager_10PlusYears", "Lead / manager (10+ years)")
                }),
                answerMapping: OnboardingAnswerMapping.WorkExperience
            ),

            // Step 11: Assessment intro
            new(
                flowDefinitionId: flowId,
                stepKey: "assessment_intro",
                title: "Quick check-in",
                stepType: OnboardingStepTypeV2.SingleChoice,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 11,
                isEnabled: true,
                description: "Two quick questions will help SpeakPath set the right starting level for you. There are no wrong answers — just pick what feels right.",
                optionsJson: JsonOptions(new[]
                {
                    ("ready", "I'm ready"),
                    ("skip", "Skip for now")
                })
            ),

            // Step 12: Assessment Q1
            new(
                flowDefinitionId: flowId,
                stepKey: "assessment_q1",
                title: "Read the sentence and choose the correct word.",
                stepType: OnboardingStepTypeV2.AssessmentQuestion,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 12,
                isEnabled: true,
                description: "She _____ to the office every day by train.",
                optionsJson: JsonOptions(new[]
                {
                    ("travel", "travel"),
                    ("travels", "travels"),
                    ("travelled", "travelled"),
                    ("is travel", "is travel")
                }),
                // AssessmentMetadataJson is server-side only — never sent to student API.
                assessmentMetadataJson: Json(new { correctAnswerKey = "travels", cefrScoreWeight = 2 })
            ),

            // Step 13: Assessment Q2
            new(
                flowDefinitionId: flowId,
                stepKey: "assessment_q2",
                title: "Choose the sentence that is correct.",
                stepType: OnboardingStepTypeV2.AssessmentQuestion,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 13,
                isEnabled: true,
                optionsJson: JsonOptions(new[]
                {
                    ("a", "Despite of the rain, we continued walking."),
                    ("b", "Although it was raining, we continued walking."),
                    ("c", "However the rain, we continued walking."),
                    ("d", "In spite the rain, we continued walking.")
                }),
                assessmentMetadataJson: Json(new { correctAnswerKey = "b", cefrScoreWeight = 3 })
            ),

            // Step 14: Summary / completion
            new(
                flowDefinitionId: flowId,
                stepKey: "summary",
                title: "You're all set!",
                stepType: OnboardingStepTypeV2.Summary,
                requirementType: OnboardingStepRequirementType.SystemRequired,
                stepOrder: 14,
                isEnabled: true,
                description: "SpeakPath has personalised your experience based on your answers. Your lessons will adapt as you progress."
            ),

            // Admin-configured example step — disabled by default.
            // To enable: update is_enabled = true via admin API (future work).
            new(
                flowDefinitionId: flowId,
                stepKey: "custom_why_learning",
                title: "Why are you learning English right now?",
                stepType: OnboardingStepTypeV2.SingleChoice,
                requirementType: OnboardingStepRequirementType.AdminConfigured,
                stepOrder: 15,
                isEnabled: false,
                description: "Optional extra question. Admin-configured.",
                optionsJson: JsonOptions(new[]
                {
                    ("new_job", "I have a new job or promotion"),
                    ("moving", "I'm moving to an English-speaking country"),
                    ("study", "I'm starting a course or university"),
                    ("personal", "Personal growth"),
                    ("other", "Something else")
                })
            )
        };
    }

    private static string Json(object obj) =>
        JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string JsonOptions(IEnumerable<(string key, string label)> options) =>
        JsonSerializer.Serialize(
            options.Select(o => new { key = o.key, label = o.label }).ToList());
}
