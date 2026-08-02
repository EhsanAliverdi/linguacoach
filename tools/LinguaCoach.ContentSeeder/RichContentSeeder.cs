using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;

namespace LinguaCoach.ContentSeeder;

/// <summary>
/// Curated-content seeder (2026-07-31) — builds a full Lesson + one-or-more multi-question
/// Exercises + a Module for a single <see cref="SkillGraphNode"/> leaf, from hand/AI-authored
/// content (not a CEFR-J CSV row). Deliberately separate from <see cref="LeafContentSeeder"/>,
/// which derives exactly one generic exercise per leaf from a single headword/definition — a poor
/// fit once a leaf needs several exercises with several questions each, matching real lesson
/// depth. Reused by every domain seeder (grammar/vocabulary/pronunciation/functional-language) in
/// this tool rather than each domain reimplementing the same Lesson/Exercise/Module wiring.
/// </summary>
public static class RichContentSeeder
{
    public static async Task<Guid> SeedLeafAsync(
        LinguaCoachDbContext db, Guid leafId, string title, string cefrLevel, string skill, string? subskill,
        int difficultyBand, string lessonBody, List<string>? examples, List<string>? commonMistakes,
        List<SeedExercise> exercises)
    {
        var lesson = new Lesson(
            title: title,
            body: lessonBody,
            sourceMode: LessonSourceMode.Manual,
            cefrLevel: cefrLevel,
            skill: skill,
            subskill: subskill,
            examplesJson: JsonSerializer.Serialize(examples ?? []),
            commonMistakesJson: JsonSerializer.Serialize(commonMistakes ?? []),
            difficultyBand: difficultyBand);
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        lesson.Approve(null);

        var module = new Module(
            title: title,
            sourceMode: ModuleSourceMode.Manual,
            description: lessonBody,
            cefrLevel: cefrLevel,
            skill: skill,
            subskill: subskill,
            difficultyBand: difficultyBand);
        db.Modules.Add(module);
        await db.SaveChangesAsync();

        db.Set<ModuleLessonLink>().Add(new ModuleLessonLink(module.Id, lesson.Id, LessonResourceRole.Primary, sortOrder: 0));

        var sortOrder = 0;
        foreach (var ex in exercises)
        {
            var (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson, activityType) = ex.Kind switch
            {
                "text" => ComposeTextSet(ex),
                "choice" => ComposeChoiceSet(ex),
                _ => throw new InvalidOperationException($"Unknown exercise kind '{ex.Kind}' for leaf '{title}'."),
            };

            var exercise = new Exercise(
                title: ex.Title,
                instructions: instructions,
                activityType: activityType,
                rendererType: ExerciseRendererType.Formio,
                sourceMode: ExerciseSourceMode.Manual,
                formSchemaJson: formSchemaJson,
                answerKeyJson: answerKeyJson,
                scoringRulesJson: scoringRulesJson,
                feedbackPlanJson: feedbackPlanJson,
                cefrLevel: cefrLevel,
                skill: skill,
                subskill: subskill,
                difficultyBand: difficultyBand,
                lessonId: lesson.Id);
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();
            exercise.Approve(null);

            db.Set<ModuleExerciseLink>().Add(new ModuleExerciseLink(
                module.Id, exercise.Id, ModuleExerciseRole.PrimaryPractice, sortOrder++));
        }

        module.Approve(null);
        db.Set<ModuleSkillGraphNodeLink>().Add(new ModuleSkillGraphNodeLink(module.Id, leafId));
        await db.SaveChangesAsync();

        return module.Id;
    }

    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson, string ActivityType)
        ComposeTextSet(SeedExercise ex)
    {
        var components = new List<object> { new { type = "content", key = "instructions", html = $"<p>{System.Net.WebUtility.HtmlEncode(ex.Instructions)}</p>" } };
        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();

        for (var i = 0; i < ex.Questions.Count; i++)
        {
            var key = $"q{i + 1}";
            var q = ex.Questions[i];
            components.Add(new { type = "content", key = $"{key}_prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(q.Prompt)}</p>" });
            components.Add(new { type = "textfield", key, label = "Your answer", input = true });
            answerKey[key] = q.Answer ?? throw new InvalidOperationException($"Text question missing 'answer': {q.Prompt}");
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.TextNormalized, CorrectAnswer: q.Answer, Points: 1.0);
        }

        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new { correctFeedback = "Correct!", incorrectFeedback = "Not quite — check the answer key." });

        return (ex.Instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson, "gap_fill");
    }

    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson, string ActivityType)
        ComposeChoiceSet(SeedExercise ex)
    {
        var components = new List<object> { new { type = "content", key = "instructions", html = $"<p>{System.Net.WebUtility.HtmlEncode(ex.Instructions)}</p>" } };
        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();

        for (var i = 0; i < ex.Questions.Count; i++)
        {
            var key = $"q{i + 1}";
            var q = ex.Questions[i];
            if (q.Options is null || q.CorrectIndex is null)
                throw new InvalidOperationException($"Choice question missing 'options'/'correctIndex': {q.Prompt}");

            var optionKeys = Enumerable.Range(0, q.Options.Count).Select(o => $"{key}_opt_{o}").ToList();
            components.Add(new
            {
                type = "radio",
                key,
                label = q.Prompt,
                input = true,
                values = q.Options.Select((text, o) => new { label = text, value = optionKeys[o] }).ToArray(),
            });

            var correctKey = optionKeys[q.CorrectIndex.Value];
            answerKey[key] = q.Options[q.CorrectIndex.Value];
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.SingleChoice, CorrectAnswer: correctKey, Points: 1.0);
        }

        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new { correctFeedback = "Correct!", incorrectFeedback = "Not quite — check the answer key." });

        return (ex.Instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson, "multiple_choice_single");
    }
}

public sealed record SeedExercise(string Title, string Instructions, string Kind, List<SeedQuestion> Questions);
public sealed record SeedQuestion(string Prompt, string? Answer = null, List<string>? Options = null, int? CorrectIndex = null);
