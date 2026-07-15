using System.Reflection;

namespace LinguaCoach.ArchitectureTests;

/// <summary>
/// Phase 2 (2026-07-15 exercise pipeline boundary consolidation) — guards against reintroducing a
/// direct "Resource Bank item(s) → Exercise" generation entry point. Every Exercise must have a
/// Lesson as its instructional parent (Resource Bank → Lesson → Exercise is the only supported
/// flow); a direct Resource-to-Exercise handler/contract silently reopens the exact bug class
/// Phase 1 (archived-Module leak / Exercise.LessonId provenance loss) and Phase 2 (this removal)
/// both closed. This does not forbid <c>ExerciseResourceLink</c> (Resource provenance on an
/// Exercise is fine and expected) — it only forbids a type whose name/shape says "generate an
/// Exercise directly from Resources with no Lesson".
/// </summary>
public class ExercisePipelineBoundaryTests
{
    // Exact type names the removed workflow used. A future reintroduction under one of these
    // names — or a request record that still carries a Resources list with no LessonId and no
    // Lesson resolution step — is exactly the pattern this guard exists to catch.
    private static readonly string[] ForbiddenTypeNames =
    {
        "IGenerateActivityFromResourcesHandler",
        "IGenerateActivityFromResourcesWithAiHandler",
        "GenerateActivityFromResourcesRequest",
        "GenerateExerciseFromResourcesRequest",
        "GenerateExerciseFromResourcesHandler",
    };

    [Fact]
    public void No_direct_resource_to_exercise_types_exist_in_domain_or_application()
    {
        var offending = new List<string>();
        foreach (var assembly in new[]
                 {
                     typeof(LinguaCoach.Domain.AssemblyMarker).Assembly,
                     typeof(LinguaCoach.Application.AssemblyMarker).Assembly,
                 })
        {
            offending.AddRange(FindForbiddenTypes(assembly));
        }

        Assert.True(offending.Count == 0,
            "Found reintroduced direct Resource-to-Exercise type(s): " + string.Join(", ", offending)
            + ". Every Exercise must have a Lesson — route through IGenerateActivityFromLessonHandler / "
            + "IGenerateActivityFromLessonWithAiHandler instead.");
    }

    [Fact]
    public void No_direct_resource_to_exercise_types_exist_in_infrastructure_or_api()
    {
        var offending = new List<string>();
        foreach (var assembly in new[]
                 {
                     typeof(LinguaCoach.Infrastructure.Exercises.ActivityGenerationService).Assembly,
                     typeof(LinguaCoach.Api.Controllers.AdminExerciseController).Assembly,
                 })
        {
            offending.AddRange(FindForbiddenTypes(assembly));
        }

        Assert.True(offending.Count == 0,
            "Found reintroduced direct Resource-to-Exercise type(s): " + string.Join(", ", offending)
            + ". Every Exercise must have a Lesson — route through IGenerateActivityFromLessonHandler / "
            + "IGenerateActivityFromLessonWithAiHandler instead.");
    }

    [Fact]
    public void No_generate_from_resources_exercise_endpoint_route_exists()
    {
        var controllerAssembly = typeof(LinguaCoach.Api.Controllers.AdminExerciseController).Assembly;
        var offending = new List<string>();

        foreach (var type in controllerAssembly.GetTypes())
        {
            if (!typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type))
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var routeAttrs = method.GetCustomAttributes(inherit: false)
                    .Select(a => a.ToString() ?? string.Empty);
                foreach (var attr in routeAttrs)
                {
                    // Exercise generation endpoints only — "generate-from-resources" under
                    // admin/lessons or admin/modules is a different, still-supported concept
                    // (Resource Bank → Lesson, Resource Bank → Module) and is not covered by this
                    // guard.
                    if (type == typeof(LinguaCoach.Api.Controllers.AdminExerciseController)
                        && attr.Contains("generate-from-resources", StringComparison.OrdinalIgnoreCase))
                    {
                        offending.Add($"{type.Name}.{method.Name}");
                    }
                }
            }
        }

        Assert.True(offending.Count == 0,
            "Found a reintroduced 'generate-from-resources' Exercise endpoint: " + string.Join(", ", offending));
    }

    private static IEnumerable<string> FindForbiddenTypes(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => ForbiddenTypeNames.Contains(t.Name))
            .Select(t => t.FullName ?? t.Name);
}
