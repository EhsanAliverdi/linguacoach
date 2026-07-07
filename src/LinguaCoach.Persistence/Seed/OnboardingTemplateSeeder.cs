using System.Text.Json;
using LinguaCoach.Application.FormIo;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds the default onboarding wizard: preferred name, support language, learning goals, focus
/// areas, and practice preferences. CEFR level is determined solely by the placement assessment
/// (StudentOnboardingFlowService still supports scoring admin-authored Quiz-tab components on
/// onboarding generically, but the seeded default no longer includes any). Idempotent: only
/// inserts a brand-new template if no onboarding template exists yet — never overwrites an
/// admin-authored template. Separately, any existing onboarding template version that has scoring but no
/// quiz-annotated AuthoringSchemaJson yet (seeded/saved before the Quiz tab existed) is backfilled
/// in place by re-embedding its existing ScoringRulesJson as quiz annotations
/// (FormIoQuizAnnotationCodec.Embed) — never touching FormIoSchemaJson/ScoringRulesJson, and never
/// touching a version an admin has since re-saved through the Quiz tab UI (which always sets
/// AuthoringSchemaJson non-null).
/// </summary>
public static class OnboardingTemplateSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existingTemplates = await db.StudentFlowTemplates
            .Include(t => t.Versions)
            .Where(t => t.FlowKind == StudentFlowKind.Onboarding)
            .ToListAsync(ct);

        if (existingTemplates.Count == 0)
        {
            var schemaJson = BuildSchemaJson();
            var authoringSchemaJson = schemaJson;

            var template = new StudentFlowTemplate(
                StudentFlowKind.Onboarding,
                "Default Onboarding",
                "System-seeded onboarding wizard: profile basics, learning goals, and practice preferences. CEFR level is determined by the placement assessment, not onboarding.");

            var version = new StudentFlowTemplateVersion(template.Id, 1, schemaJson, Guid.Empty, scoringRulesJson: null);
            version.SetAuthoringSchema(authoringSchemaJson);

            template.AddVersion(version);
            version.Publish();
            template.SetActiveVersion(version.Id);

            db.StudentFlowTemplates.Add(template);
            db.StudentFlowTemplateVersions.Add(version);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Seeded default onboarding template {TemplateId} (published version {VersionId}).",
                template.Id, version.Id);
            return;
        }

        var dirty = false;
        foreach (var version in existingTemplates.SelectMany(t => t.Versions))
        {
            if (!string.IsNullOrWhiteSpace(version.AuthoringSchemaJson)) continue;
            if (string.IsNullOrWhiteSpace(version.ScoringRulesJson)) continue;

            version.SetAuthoringSchema(FormIoQuizAnnotationCodec.Embed(version.FormIoSchemaJson, version.ScoringRulesJson));
            dirty = true;
        }
        if (dirty) await db.SaveChangesAsync(ct);
    }

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

        var schema = new
        {
            display = "wizard",
            components = new object[]
            {
                aboutYouPage, goalsPage, practicePrefsPage
            }
        };

        return JsonSerializer.Serialize(schema);
    }
}
