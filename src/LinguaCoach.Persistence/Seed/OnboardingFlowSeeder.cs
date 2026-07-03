using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Questions;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

// Seeds the default onboarding v2 flow. Idempotent: re-runs are safe.
// Flows are immutable once students have progress. A new version creates a new flow.
// Admin-configured steps are seeded disabled by default.
//
// Unified Question-Schema Phase 6b: steps are grouped into categories and authored via the
// shared QuestionContent schema (SingleChoice/MultipleChoice/FreeText + AnswerMapping) instead
// of one-off StepTypes per semantic field — a CEFR-scored question is just a SingleChoice with
// a CorrectAnswerKey, "support language" is a SingleChoice whose choices are sourced dynamically
// from the Languages table (OptionsSource), and "work experience" is two independent SingleChoice
// steps instead of one composite step.
public static class OnboardingFlowSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db)
    {
        var existingActive = await db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.IsActive);

        // Only ever reconcile the seeder's own "Default Flow" — an admin may have built and
        // activated a genuinely custom flow via /admin/onboarding, which must never be touched
        // or replaced by this seeder regardless of its step set.
        if (existingActive is not null && existingActive.Name == "Default Flow")
        {
            var (placeholderSteps, _) = BuildDefaultFlow(existingActive.Id);
            var currentSignatures = existingActive.Steps
                .Select(s => (s.StepKey, s.OptionsJson)).ToHashSet();
            var targetSignatures = placeholderSteps
                .Select(s => (s.StepKey, s.OptionsJson)).ToHashSet();
            if (targetSignatures.SetEquals(currentSignatures)) return; // already up to date

            var newFlow = new OnboardingFlowDefinition("Default Flow", version: existingActive.Version + 1);
            var (newSteps, newCategories) = BuildDefaultFlow(newFlow.Id);
            ValidateNoDuplicateKeys(newSteps);
            foreach (var category in newCategories)
                newFlow.AddCategory(category);
            foreach (var step in newSteps)
                newFlow.AddStep(step);

            existingActive.Deactivate();
            newFlow.Activate();
            db.OnboardingFlowDefinitions.Add(newFlow);
            await db.SaveChangesAsync();
            return;
        }

        // An active flow exists but isn't ours (admin-built custom flow) — leave it alone.
        if (existingActive is not null) return;

        var flow = new OnboardingFlowDefinition("Default Flow", version: 1);
        flow.Activate();

        var (steps, categories) = BuildDefaultFlow(flow.Id);
        ValidateNoDuplicateKeys(steps);

        foreach (var category in categories)
            flow.AddCategory(category);
        foreach (var step in steps)
            flow.AddStep(step);

        db.OnboardingFlowDefinitions.Add(flow);
        await db.SaveChangesAsync();
    }

    private static void ValidateNoDuplicateKeys(List<OnboardingStepDefinition> steps)
    {
        var duplicates = steps.Select(s => s.StepKey)
            .GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Any())
            throw new InvalidOperationException($"Duplicate step keys in default flow: {string.Join(", ", duplicates)}");
    }

    private static (List<OnboardingStepDefinition> Steps, List<OnboardingCategoryDefinition> Categories) BuildDefaultFlow(Guid flowId)
    {
        var categories = new List<OnboardingCategoryDefinition>
        {
            new(flowId, "Welcome", categoryOrder: 1),
            new(flowId, "About you", categoryOrder: 2),
            new(flowId, "Goals", categoryOrder: 3),
            new(flowId, "Preferences", categoryOrder: 4),
            new(flowId, "Work context", categoryOrder: 5, description: "Only shown if your goals mention work."),
            new(flowId, "Quick check", categoryOrder: 6),
            new(flowId, "Summary", categoryOrder: 7),
        };

        var welcome = categories[0];
        var aboutYou = categories[1];
        var goals = categories[2];
        var preferences = categories[3];
        var workContext = categories[4];
        var quickCheck = categories[5];
        var summary = categories[6];

        var steps = new List<OnboardingStepDefinition>
        {
            BuildStep(flowId, welcome.Id, "welcome", "Welcome to SpeakPath",
                OnboardingStepTypeV2.Welcome, OnboardingStepRequirementType.SystemRequired, 1,
                description: "Let's set up your personalised English learning experience. This will only take a few minutes."),

            BuildStep(flowId, aboutYou.Id, "preferred_name", "What should we call you?",
                OnboardingStepTypeV2.FreeText, OnboardingStepRequirementType.SystemRequired, 2,
                description: "Enter the name you'd like SpeakPath to use when talking to you.",
                answerMapping: OnboardingAnswerMapping.PreferredName,
                content: new FreeTextQuestion { QuestionText = "What should we call you?", MaxLength = 100 }),

            BuildStep(flowId, aboutYou.Id, "support_language", "Would you like support in another language?",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.SystemRequired, 3,
                description: "If you'd like explanations in your first language when something is unclear, select it here. This is optional — SpeakPath teaches in English.",
                answerMapping: OnboardingAnswerMapping.SupportLanguage,
                content: new SingleChoiceQuestion { QuestionText = "Would you like support in another language?", Choices = [], OptionsSource = "languages" }),

            BuildStep(flowId, goals.Id, "learning_goals", "What do you want to use English for?",
                OnboardingStepTypeV2.MultipleChoice, OnboardingStepRequirementType.SystemRequired, 4,
                description: "Select all that apply. This helps SpeakPath personalise your practice.",
                answerMapping: OnboardingAnswerMapping.LearningGoals,
                content: new MultipleChoiceQuestion
                {
                    QuestionText = "What do you want to use English for?",
                    Choices =
                    [
                        new() { Key = "day_to_day", Label = "Day-to-day conversations" },
                        new() { Key = "work", Label = "Work and professional communication" },
                        new() { Key = "study", Label = "Study or academic English" },
                        new() { Key = "travel", Label = "Travel" },
                        new() { Key = "job_interview", Label = "Job interviews" },
                        new() { Key = "social", Label = "Social situations" },
                        new() { Key = "migration", Label = "Migration or settlement" },
                        new() { Key = "writing_confidence", Label = "Writing confidence" },
                        new() { Key = "listening_confidence", Label = "Listening and understanding" },
                        new() { Key = "pronunciation", Label = "Pronunciation and speaking clearly" },
                    ],
                }),

            BuildStep(flowId, goals.Id, "custom_learning_goal", "Anything else you'd like to use English for?",
                OnboardingStepTypeV2.FreeText, OnboardingStepRequirementType.AdminConfigured, 5,
                description: "Optional — describe it in your own words.",
                answerMapping: OnboardingAnswerMapping.CustomLearningGoal,
                content: new FreeTextQuestion { QuestionText = "Anything else you'd like to use English for?", MaxLength = 200 }),

            BuildStep(flowId, goals.Id, "focus_areas", "Where would you like the most practice?",
                OnboardingStepTypeV2.MultipleChoice, OnboardingStepRequirementType.SystemRequired, 6,
                description: "Select your top areas. SpeakPath will prioritise these in your lessons.",
                answerMapping: OnboardingAnswerMapping.FocusAreas,
                content: new MultipleChoiceQuestion
                {
                    QuestionText = "Where would you like the most practice?",
                    Choices =
                    [
                        new() { Key = "speaking", Label = "Speaking" },
                        new() { Key = "listening", Label = "Listening" },
                        new() { Key = "writing", Label = "Writing" },
                        new() { Key = "reading", Label = "Reading" },
                        new() { Key = "vocabulary", Label = "Vocabulary" },
                        new() { Key = "grammar", Label = "Grammar" },
                    ],
                }),

            BuildStep(flowId, goals.Id, "custom_focus_area", "Any other area you'd like to focus on?",
                OnboardingStepTypeV2.FreeText, OnboardingStepRequirementType.AdminConfigured, 7,
                description: "Optional — describe it in your own words.",
                answerMapping: OnboardingAnswerMapping.CustomFocusArea,
                content: new FreeTextQuestion { QuestionText = "Any other area you'd like to focus on?", MaxLength = 200 }),

            BuildStep(flowId, goals.Id, "learning_goal_description", "Anything else you'd like to tell us?",
                OnboardingStepTypeV2.FreeText, OnboardingStepRequirementType.AdminConfigured, 8,
                description: "Optional — describe a real situation you find difficult. You can write in your own language too.",
                answerMapping: OnboardingAnswerMapping.LearningGoalDescription,
                content: new FreeTextQuestion { QuestionText = "Anything else you'd like to tell us?", MaxLength = 1000, IsMultiline = true }),

            BuildStep(flowId, preferences.Id, "difficulty_preference", "How challenging should your practice feel?",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.SystemRequired, 9,
                description: "You can change this at any time from your profile.",
                answerMapping: OnboardingAnswerMapping.DifficultyPreference,
                // Option keys must match DifficultyPreference enum member names exactly.
                content: new SingleChoiceQuestion
                {
                    QuestionText = "How challenging should your practice feel?",
                    Choices =
                    [
                        new() { Key = "Gentle", Label = "Gentle — I want to build confidence gradually" },
                        new() { Key = "Balanced", Label = "Moderate — a steady challenge is good for me" },
                        new() { Key = "Challenging", Label = "Challenging — push me to improve quickly" },
                    ],
                }),

            BuildStep(flowId, preferences.Id, "session_duration", "How much time do you want to spend in each lesson?",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.SystemRequired, 10,
                description: "We'll use this to build lessons that fit your schedule.",
                answerMapping: OnboardingAnswerMapping.SessionDuration,
                content: new SingleChoiceQuestion
                {
                    QuestionText = "How much time do you want to spend in each lesson?",
                    Choices =
                    [
                        new() { Key = "10", Label = "10 minutes — quick daily check-in" },
                        new() { Key = "15", Label = "15 minutes — focused short practice" },
                        new() { Key = "20", Label = "20 minutes — balanced daily session" },
                        new() { Key = "30", Label = "30 minutes — deep practice session" },
                    ],
                }),

            // Only shown to students whose learning_goals included "work" (see
            // OnboardingV2StepHandler.WorkOnlyStepKeys). AdminConfigured so it never blocks
            // completion for students who skip it.
            BuildStep(flowId, workContext.Id, "career_context", "What is your job, field, or target workplace context?",
                OnboardingStepTypeV2.FreeText, OnboardingStepRequirementType.AdminConfigured, 11,
                description: "SpeakPath uses your workplace context to create realistic, relevant practice scenarios.",
                answerMapping: OnboardingAnswerMapping.CareerContext,
                content: new FreeTextQuestion { QuestionText = "What is your job, field, or target workplace context?", MaxLength = 200, IsMultiline = true }),

            // Work experience (Phase 6b: split into two independent SingleChoice steps instead
            // of one composite step — StudentProfile.SetProfessionalExperienceLevel/SetRoleFamiliarity
            // each set their own field, recomputing WorkplaceSeniority once both are known).
            BuildStep(flowId, workContext.Id, "professional_experience_level", "How many years of professional experience do you have?",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.AdminConfigured, 12,
                answerMapping: OnboardingAnswerMapping.ProfessionalExperienceLevel,
                content: new SingleChoiceQuestion
                {
                    QuestionText = "How many years of professional experience do you have?",
                    Choices =
                    [
                        new() { Key = "NoProfessionalExperience", Label = "No professional experience yet" },
                        new() { Key = "EntryLevelOrGraduate", Label = "Entry level / graduate" },
                        new() { Key = "Junior_0_2Years", Label = "Junior (0-2 years)" },
                        new() { Key = "MidLevel_2_5Years", Label = "Mid-level (2-5 years)" },
                        new() { Key = "Senior_5_10Years", Label = "Senior (5-10 years)" },
                        new() { Key = "LeadOrManager_10PlusYears", Label = "Lead / manager (10+ years)" },
                    ],
                }),

            BuildStep(flowId, workContext.Id, "role_familiarity", "How familiar are you with your current type of role?",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.AdminConfigured, 13,
                answerMapping: OnboardingAnswerMapping.RoleFamiliarity,
                content: new SingleChoiceQuestion
                {
                    QuestionText = "How familiar are you with your current type of role?",
                    Choices =
                    [
                        new() { Key = "NewToRole", Label = "New to this type of role" },
                        new() { Key = "UnderstandsBasics", Label = "Understand the basics" },
                        new() { Key = "CurrentlyWorkingInRole", Label = "Currently working in this role" },
                        new() { Key = "ExperiencedInRole", Label = "Experienced in this role" },
                        new() { Key = "ManagesOrTrainsOthers", Label = "Manage or train others in this role" },
                    ],
                }),

            BuildStep(flowId, quickCheck.Id, "assessment_intro", "Quick check-in",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.SystemRequired, 14,
                description: "Two quick questions will help SpeakPath set the right starting level for you. There are no wrong answers — just pick what feels right.",
                content: new SingleChoiceQuestion
                {
                    QuestionText = "Quick check-in",
                    Choices = [new() { Key = "ready", Label = "I'm ready" }, new() { Key = "skip", Label = "Skip for now" }],
                }),

            // CEFR-scored: a SingleChoice with CorrectAnswerKey set, scored via the shared
            // IQuestionScorer (identical code path to placement) — no separate "AssessmentQuestion"
            // step type needed.
            BuildStep(flowId, quickCheck.Id, "assessment_q1", "Read the sentence and choose the correct word.",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.SystemRequired, 15,
                description: "She _____ to the office every day by train.",
                content: new SingleChoiceQuestion
                {
                    QuestionText = "Read the sentence and choose the correct word.",
                    Choices =
                    [
                        new() { Key = "travel", Label = "travel" }, new() { Key = "travels", Label = "travels" },
                        new() { Key = "travelled", Label = "travelled" }, new() { Key = "is travel", Label = "is travel" },
                    ],
                    CorrectAnswerKey = "travels",
                }),

            BuildStep(flowId, quickCheck.Id, "assessment_q2", "Choose the sentence that is correct.",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.SystemRequired, 16,
                content: new SingleChoiceQuestion
                {
                    QuestionText = "Choose the sentence that is correct.",
                    Choices =
                    [
                        new() { Key = "a", Label = "Despite of the rain, we continued walking." },
                        new() { Key = "b", Label = "Although it was raining, we continued walking." },
                        new() { Key = "c", Label = "However the rain, we continued walking." },
                        new() { Key = "d", Label = "In spite the rain, we continued walking." },
                    ],
                    CorrectAnswerKey = "b",
                }),

            BuildStep(flowId, summary.Id, "summary", "You're all set!",
                OnboardingStepTypeV2.Summary, OnboardingStepRequirementType.SystemRequired, 17,
                description: "SpeakPath has personalised your experience based on your answers. Your lessons will adapt as you progress."),

            // Admin-configured example step — disabled by default.
            BuildStep(flowId, preferences.Id, "custom_why_learning", "Why are you learning English right now?",
                OnboardingStepTypeV2.SingleChoice, OnboardingStepRequirementType.AdminConfigured, 18,
                isEnabled: false,
                description: "Optional extra question. Admin-configured.",
                content: new SingleChoiceQuestion
                {
                    QuestionText = "Why are you learning English right now?",
                    Choices =
                    [
                        new() { Key = "new_job", Label = "I have a new job or promotion" },
                        new() { Key = "moving", Label = "I'm moving to an English-speaking country" },
                        new() { Key = "study", Label = "I'm starting a course or university" },
                        new() { Key = "personal", Label = "Personal growth" },
                        new() { Key = "other", Label = "Something else" },
                    ],
                }),
        };

        return (steps, categories);
    }

    private static OnboardingStepDefinition BuildStep(
        Guid flowId, Guid categoryId, string stepKey, string title,
        OnboardingStepTypeV2 stepType, OnboardingStepRequirementType requirementType, int stepOrder,
        string? description = null, bool isEnabled = true,
        OnboardingAnswerMapping answerMapping = OnboardingAnswerMapping.None,
        QuestionContent? content = null)
    {
        var (optionsJson, validationMetadataJson) = OnboardingContentConverter.ToLegacyFields(content);

        var step = new OnboardingStepDefinition(
            flowDefinitionId: flowId,
            stepKey: stepKey,
            title: title,
            stepType: stepType,
            requirementType: requirementType,
            stepOrder: stepOrder,
            isEnabled: isEnabled,
            description: description,
            optionsJson: optionsJson,
            validationMetadataJson: validationMetadataJson,
            answerMapping: answerMapping,
            categoryId: categoryId);

        if (content is not null) step.SetContent(content);
        return step;
    }
}
