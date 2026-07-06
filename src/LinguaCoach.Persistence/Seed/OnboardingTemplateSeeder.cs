using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds the default onboarding wizard: preferred name, support language, learning goals, focus
/// areas, practice preferences, and a 10-question CEFR quick check (assessment_q1..assessment_q10,
/// scored by StudentOnboardingFlowService against ScoringRulesJson). Idempotent: only runs if no
/// onboarding template exists yet — never overwrites an admin-authored template.
/// </summary>
public static class OnboardingTemplateSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var exists = await db.StudentFlowTemplates.AnyAsync(t => t.FlowKind == StudentFlowKind.Onboarding, ct);
        if (exists) return;

        var template = new StudentFlowTemplate(
            StudentFlowKind.Onboarding,
            "Default Onboarding",
            "System-seeded onboarding wizard: profile basics, learning goals, practice preferences, and a 10-question CEFR quick check.");

        var version = new StudentFlowTemplateVersion(
            template.Id, 1, BuildSchemaJson(), Guid.Empty, BuildScoringRulesJson());

        template.AddVersion(version);
        version.Publish();
        template.SetActiveVersion(version.Id);

        db.StudentFlowTemplates.Add(template);
        db.StudentFlowTemplateVersions.Add(version);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded default onboarding template {TemplateId} (published version {VersionId}).",
            template.Id, version.Id);
    }

    private static object Radio(string key, string label, (string value, string label)[] options) => new
    {
        type = "radio",
        key,
        label,
        input = true,
        tableView = true,
        values = options.Select(o => new { label = o.label, value = o.value }).ToArray(),
        validate = new { required = true }
    };

    private static string BuildSchemaJson()
    {
        var aboutYouPage = new
        {
            type = "panel",
            key = "page_about_you",
            title = "About You",
            label = "About You",
            breadcrumb = "About You",
            components = new object[]
            {
                new
                {
                    type = "textfield", key = "preferred_name", label = "What should we call you?",
                    input = true, tableView = true, validate = new { required = true }
                },
                new
                {
                    type = "radio", key = "support_language_needed",
                    label = "Do you need help in another language?",
                    input = true, tableView = true,
                    values = new object[]
                    {
                        new { label = "Yes", value = "yes" },
                        new { label = "No", value = "no" }
                    },
                    defaultValue = "no",
                    validate = new { required = true }
                },
                new
                {
                    type = "select", key = "support_language",
                    label = "Which language would you like support in?",
                    input = true, tableView = true,
                    data = new
                    {
                        values = new object[]
                        {
                            new { label = "Persian (فارسی)", value = "fa" }
                        }
                    },
                    conditional = new { show = true, when = "support_language_needed", eq = "yes" },
                    validate = new { required = true }
                }
            }
        };

        var goalsPage = new
        {
            type = "panel",
            key = "page_goals",
            title = "Your Goals",
            label = "Your Goals",
            breadcrumb = "Your Goals",
            components = new object[]
            {
                new
                {
                    type = "selectboxes", key = "learning_goals",
                    label = "What are your learning goals? (select all that apply)",
                    input = true, tableView = true,
                    values = new object[]
                    {
                        new { label = "Career advancement", value = "career" },
                        new { label = "Travel", value = "travel" },
                        new { label = "Academic study", value = "academic" },
                        new { label = "Everyday conversation", value = "everyday" },
                        new { label = "Exam preparation", value = "exam" },
                        new { label = "Other", value = "other" }
                    },
                    validate = new { required = true }
                },
                new
                {
                    type = "textfield", key = "custom_learning_goal",
                    label = "What other goals do you have?",
                    input = true, tableView = true,
                    conditional = new { show = true, when = "learning_goals", eq = "other" },
                    validate = new { required = true }
                },
                new
                {
                    type = "selectboxes", key = "focus_areas",
                    label = "Which skills do you want to focus on? (select all that apply)",
                    input = true, tableView = true,
                    values = new object[]
                    {
                        new { label = "Speaking", value = "speaking" },
                        new { label = "Listening", value = "listening" },
                        new { label = "Reading", value = "reading" },
                        new { label = "Writing", value = "writing" },
                        new { label = "Grammar", value = "grammar" },
                        new { label = "Vocabulary", value = "vocabulary" },
                        new { label = "Other", value = "other" }
                    },
                    validate = new { required = true }
                },
                new
                {
                    type = "textfield", key = "custom_focus_area",
                    label = "What other focus area would you like?",
                    input = true, tableView = true,
                    conditional = new { show = true, when = "focus_areas", eq = "other" },
                    validate = new { required = true }
                }
            }
        };

        var practicePrefsPage = new
        {
            type = "panel",
            key = "page_practice_preferences",
            title = "Practice Preferences",
            label = "Practice Preferences",
            breadcrumb = "Practice Preferences",
            components = new object[]
            {
                new
                {
                    type = "radio", key = "difficulty_preference",
                    label = "How challenging do you want your practice to be?",
                    input = true, tableView = true,
                    values = new object[]
                    {
                        new { label = "Gentle — build confidence steadily", value = "Gentle" },
                        new { label = "Balanced — a mix of practice and challenge", value = "Balanced" },
                        new { label = "Challenging — push me", value = "Challenging" }
                    },
                    validate = new { required = true }
                },
                new
                {
                    type = "select", key = "session_duration",
                    label = "How long do you want each practice session to be?",
                    input = true, tableView = true,
                    data = new
                    {
                        values = new object[]
                        {
                            new { label = "10 minutes", value = "10" },
                            new { label = "15 minutes", value = "15" },
                            new { label = "20 minutes", value = "20" },
                            new { label = "30 minutes", value = "30" },
                            new { label = "45 minutes", value = "45" }
                        }
                    },
                    defaultValue = "15",
                    validate = new { required = true }
                }
            }
        };

        var quickCheckPart1 = new
        {
            type = "panel",
            key = "page_quick_check_1",
            title = "Quick Check — Part 1",
            label = "Quick Check — Part 1",
            breadcrumb = "Quick Check 1",
            components = new object[]
            {
                Radio("assessment_q1", "'They ___ from Canada.'",
                    new[] { ("a", "is"), ("b", "are"), ("c", "am") }),
                Radio("assessment_q2", "'He ___ to work every day.'",
                    new[] { ("a", "go"), ("b", "goes"), ("c", "going") }),
                Radio("assessment_q3", "What does 'purchase' mean?",
                    new[] { ("a", "to buy"), ("b", "to sell"), ("c", "to break") }),
                Radio("assessment_q4", "Which word is closest in meaning to 'assist'?",
                    new[] { ("a", "help"), ("b", "stop"), ("c", "avoid") }),
                Radio("assessment_q5", "'Yesterday, we ___ to the market.'",
                    new[] { ("a", "go"), ("b", "went"), ("c", "gone") })
            }
        };

        var quickCheckPart2 = new
        {
            type = "panel",
            key = "page_quick_check_2",
            title = "Quick Check — Part 2",
            label = "Quick Check — Part 2",
            breadcrumb = "Quick Check 2",
            components = new object[]
            {
                Radio("assessment_q6", "Read: 'The train leaves at 9 and arrives at 11.' How long is the journey?",
                    new[] { ("a", "1 hour"), ("b", "2 hours"), ("c", "3 hours") }),
                Radio("assessment_q7", "'If I ___ more time, I would travel more.'",
                    new[] { ("a", "have"), ("b", "had"), ("c", "has") }),
                Radio("assessment_q8", "What does 'reluctant' mean?",
                    new[] { ("a", "eager"), ("b", "unwilling"), ("c", "certain") }),
                Radio("assessment_q9", "'The report ___ by the manager before the meeting.'",
                    new[] { ("a", "will review"), ("b", "will be reviewed"), ("c", "reviewed") }),
                Radio("assessment_q10", "What is the best synonym for 'meticulous'?",
                    new[] { ("a", "careless"), ("b", "thorough"), ("c", "quick") })
            }
        };

        var schema = new
        {
            display = "wizard",
            components = new object[]
            {
                aboutYouPage, goalsPage, practicePrefsPage, quickCheckPart1, quickCheckPart2
            }
        };

        return JsonSerializer.Serialize(schema);
    }

    private static string BuildScoringRulesJson()
    {
        var answers = new Dictionary<string, string>
        {
            ["assessment_q1"] = "b",
            ["assessment_q2"] = "b",
            ["assessment_q3"] = "a",
            ["assessment_q4"] = "a",
            ["assessment_q5"] = "b",
            ["assessment_q6"] = "b",
            ["assessment_q7"] = "b",
            ["assessment_q8"] = "b",
            ["assessment_q9"] = "b",
            ["assessment_q10"] = "b"
        };

        var rules = answers.ToDictionary(
            kv => kv.Key,
            kv => new { correctAnswerKey = kv.Value });

        return JsonSerializer.Serialize(rules);
    }
}
