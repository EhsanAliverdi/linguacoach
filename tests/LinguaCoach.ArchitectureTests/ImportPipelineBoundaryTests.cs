using System.Reflection;
using LinguaCoach.Application.ResourceImport;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;

namespace LinguaCoach.ArchitectureTests;

/// <summary>
/// Phase 4.2 (2026-07-15 mandatory Import Execution Plan gate for every import) — guards against
/// reintroducing an ungated Import path. The Phase 4.1 audit's central finding was that the
/// pre-existing single-file/paste pipeline could create, AI-enrich, and publish candidates with no
/// ImportPackage/ImportProfile involved at all; this phase removed that pipeline's public entry
/// points entirely. These tests fail the build if any of those entry points, or their now-deleted
/// types, come back.
/// </summary>
public class ImportPipelineBoundaryTests
{
    // Exact type names the removed ungated workflow used. Reintroducing any of these under these
    // names is exactly the regression this guard exists to catch.
    private static readonly string[] ForbiddenTypeNames =
    {
        "AdminContentImportController",
        "ContentImportService",
        "IContentImportService",
        "ContentImportRequest",
        "ContentImportResult",
        "BatchApproveAndPublishResourceCandidatesCommand",
    };

    [Fact]
    public void No_removed_ungated_import_types_exist_in_any_layer()
    {
        var offending = new List<string>();
        foreach (var assembly in new[]
                 {
                     typeof(LinguaCoach.Domain.AssemblyMarker).Assembly,
                     typeof(LinguaCoach.Application.AssemblyMarker).Assembly,
                     typeof(LinguaCoach.Infrastructure.DependencyInjection).Assembly,
                     typeof(LinguaCoach.Api.Controllers.AdminImportPackageController).Assembly,
                 })
        {
            offending.AddRange(assembly.GetTypes()
                .Where(t => ForbiddenTypeNames.Contains(t.Name))
                .Select(t => t.FullName ?? t.Name));
        }

        Assert.True(offending.Count == 0,
            "Found a reintroduced ungated-import type: " + string.Join(", ", offending) +
            ". Every Import must go through AdminImportPackageController / IImportPackageSubmissionService " +
            "and an approved Import Execution Plan.");
    }

    /// <summary>No public API controller may directly depend on the internal candidate-creation/
    /// AI-enrichment services — those may only be called by ImportPackageProcessingService, itself
    /// only reachable via the plan-gated background job.</summary>
    [Fact]
    public void No_controller_directly_depends_on_the_internal_import_or_analysis_services()
    {
        var forbiddenDependencyTypes = new[]
        {
            typeof(IResourceImportService),
            typeof(IResourceCandidateBatchAnalysisService),
        };

        var controllerAssembly = typeof(LinguaCoach.Api.Controllers.AdminImportPackageController).Assembly;
        var offending = new List<string>();

        foreach (var type in controllerAssembly.GetTypes())
        {
            if (!typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type)) continue;

            foreach (var ctor in type.GetConstructors())
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (forbiddenDependencyTypes.Contains(param.ParameterType))
                        offending.Add($"{type.Name} depends on {param.ParameterType.Name}");
                }
            }
        }

        Assert.True(offending.Count == 0,
            "Found a controller with a direct dependency on an internal import-processing service " +
            "(bypassing the plan gate): " + string.Join(", ", offending));
    }

    /// <summary>No public route may match the shape of a removed ungated endpoint (direct file-
    /// upload import, propose-mapping, batch candidate analysis, approve-and-publish).</summary>
    [Fact]
    public void No_removed_ungated_import_routes_exist()
    {
        var forbiddenRouteFragments = new[]
        {
            "content-imports",
            "propose-mapping",
            "candidates/analyze",
            "approve-and-publish",
        };

        var controllerAssembly = typeof(LinguaCoach.Api.Controllers.AdminImportPackageController).Assembly;
        var offending = new List<string>();

        foreach (var type in controllerAssembly.GetTypes())
        {
            if (!typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type)) continue;

            var classRouteAttrs = type.GetCustomAttributes(inherit: false).Select(a => a.ToString() ?? string.Empty);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var methodRouteAttrs = method.GetCustomAttributes(inherit: false).Select(a => a.ToString() ?? string.Empty);
                foreach (var attr in classRouteAttrs.Concat(methodRouteAttrs))
                {
                    foreach (var fragment in forbiddenRouteFragments)
                    {
                        if (attr.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                            offending.Add($"{type.Name}.{method.Name} ({fragment})");
                    }
                }
            }
        }

        Assert.True(offending.Count == 0,
            "Found a reintroduced ungated-import route: " + string.Join(", ", offending));
    }

    /// <summary>Positive check — the one public entry point that may create an ImportPackage
    /// (AdminImportPackageController) is the only controller depending on
    /// IImportPackageSubmissionService/IImportPackageUploadService.</summary>
    [Fact]
    public void Only_the_import_package_controller_can_create_an_import_package()
    {
        var packageCreationDependencyTypes = new[]
        {
            typeof(IImportPackageSubmissionService),
            typeof(IImportPackageUploadService),
        };

        var controllerAssembly = typeof(LinguaCoach.Api.Controllers.AdminImportPackageController).Assembly;
        var offending = new List<string>();

        foreach (var type in controllerAssembly.GetTypes())
        {
            if (!typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type)) continue;
            if (type == typeof(LinguaCoach.Api.Controllers.AdminImportPackageController)) continue;

            foreach (var ctor in type.GetConstructors())
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (packageCreationDependencyTypes.Contains(param.ParameterType))
                        offending.Add($"{type.Name} depends on {param.ParameterType.Name}");
                }
            }
        }

        Assert.True(offending.Count == 0,
            "Found a second controller that can create an ImportPackage: " + string.Join(", ", offending) +
            ". AdminImportPackageController must remain the sole entry point.");
    }

    // ── Phase 4.3 (2026-07-16) — approved-plan-driven execution guards. The Phase 4.1 audit's
    // central finding for this phase was that ImportPackageProcessingService never read the
    // approved plan's mapping/routing decisions and instead re-derived them independently. These
    // tests fail the build if that disconnect comes back. ──

    /// <summary>Package execution must resolve its routing/mapping instructions through the
    /// single typed resolver — never construct them ad hoc.</summary>
    [Fact]
    public void Package_processing_service_depends_on_the_approved_profile_resolver()
    {
        var result = Types.InAssembly(typeof(LinguaCoach.Infrastructure.DependencyInjection).Assembly)
            .That().HaveNameEndingWith("ImportPackageProcessingService")
            .Should().HaveDependencyOn(typeof(IApprovedImportProfileResolver).FullName!)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "ImportPackageProcessingService must depend on IApprovedImportProfileResolver to drive execution " +
            "from the approved plan. " + FailureSummary(result));
    }

    /// <summary>Package execution must never call plan-generation/mapping-inference services
    /// itself — those are pre-approval, proposal-only concerns. Reintroducing this dependency is
    /// exactly the "execution re-derives its own routing" regression Phase 4.3 fixed.</summary>
    [Fact]
    public void Package_processing_service_does_not_depend_on_plan_generation_or_mapping_inference()
    {
        var result = Types.InAssembly(typeof(LinguaCoach.Infrastructure.DependencyInjection).Assembly)
            .That().HaveNameEndingWith("ImportPackageProcessingService")
            .Should().NotHaveDependencyOnAny(
                typeof(IImportExecutionPlanGenerationService).FullName!,
                typeof(IResourceImportColumnMappingService).FullName!,
                "LinguaCoach.Infrastructure.ResourceImport.ImportExecutionPlanGenerationService")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "ImportPackageProcessingService must not depend on plan-generation or AI mapping-proposal " +
            "services — routing/mapping must come from the approved plan via IApprovedImportProfileResolver, " +
            "not be re-inferred at execution time. " + FailureSummary(result));
    }

    /// <summary>Only the types that legitimately produce (plan generation), parse/validate (the
    /// resolver), or consume (execution) the approved-plan execution contract may reference it —
    /// guards against a new service growing its own independent ProfileJson parsing instead of
    /// going through IApprovedImportProfileResolver.</summary>
    [Fact]
    public void Only_known_types_depend_on_the_execution_group_instruction_contract()
    {
        var allowedTypeNames = new HashSet<string>
        {
            "ApprovedImportProfileResolver", // parses/validates ProfileJson — the one allowed parser
            "ImportExecutionPlanGenerationService", // produces the instructions at plan-generation time
            "ImportPackageProcessingService", // consumes the resolved, typed instructions
            // Phase 4.4 (Workstream A) — admin editing of the same typed contract before approval.
            "ImportPlanDraftService", // validates/persists an admin's edited instructions
            "ImportPlanEstimateService", // recalculates volume/cost from a candidate instruction set
            "ImportPlanPreviewService", // bounded preview using the same instructions
            "ImportPlanDtoHelpers", // builds the DTO's GroupInstructions field for display
        };

        var offendingTypes = Types.InAssembly(typeof(LinguaCoach.Infrastructure.DependencyInjection).Assembly)
            .That().HaveDependencyOn(typeof(ImportExecutionGroupInstruction).FullName!)
            .GetTypes()
            .Where(t => !allowedTypeNames.Contains(t.Name))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        Assert.True(offendingTypes.Count == 0,
            "Found a type depending directly on ImportExecutionGroupInstruction outside the known " +
            "producer/parser/consumer set — route it through IApprovedImportProfileResolver instead: " +
            string.Join(", ", offendingTypes));
    }

    private static string FailureSummary(TestResult result) =>
        result.FailingTypeNames is null
            ? "Architecture rule failed."
            : "Failing type(s): " + string.Join(", ", result.FailingTypeNames);

    // ── Phase 4.4 (2026-07-16) — editable plans and durable cost accounting guards. ──

    /// <summary>Workstream requirement: "controllers do not calculate Import cost." The
    /// controller may depend on the plan-editing/estimate/draft services (which return already-
    /// computed DTOs) but never on the pricing resolver or cost-estimation options directly —
    /// those belong to <c>ImportPlanEstimateService</c>/<c>ImportExecutionPlanApprovalService</c>/
    /// <c>ImportPackageProcessingService</c>.</summary>
    [Fact]
    public void Import_package_controller_does_not_depend_on_pricing_or_cost_options_directly()
    {
        var forbidden = new[]
        {
            typeof(LinguaCoach.Application.Ai.IAiPricingResolver),
            typeof(LinguaCoach.Infrastructure.ResourceImport.ImportCostEstimationOptions),
        };

        var controllerType = typeof(LinguaCoach.Api.Controllers.AdminImportPackageController);
        var offending = new List<string>();
        foreach (var ctor in controllerType.GetConstructors())
        {
            foreach (var param in ctor.GetParameters())
            {
                if (forbidden.Contains(param.ParameterType))
                    offending.Add($"{controllerType.Name} depends on {param.ParameterType.Name}");
            }
        }

        Assert.True(offending.Count == 0,
            "AdminImportPackageController must not calculate cost itself: " + string.Join(", ", offending));
    }

    /// <summary>Workstream requirement: "Import provider calls pass through durable operation/
    /// accounting orchestration" — no controller may depend on the STT provider or the STT ledger
    /// directly; only <c>ImportPackageProcessingService</c> orchestrates that call sequence.
    /// </summary>
    [Fact]
    public void Import_package_controller_does_not_depend_on_the_STT_ledger_directly()
    {
        // Scoped to AdminImportPackageController specifically (not every controller) — other
        // features (e.g. speaking practice) have their own legitimate, unrelated reasons to depend
        // on ISpeechToTextService; this guard is only about the Import pipeline's own orchestration
        // boundary.
        var controllerType = typeof(LinguaCoach.Api.Controllers.AdminImportPackageController);
        var offending = new List<string>();
        foreach (var ctor in controllerType.GetConstructors())
        {
            foreach (var param in ctor.GetParameters())
            {
                if (param.ParameterType == typeof(IImportSttOperationLedger))
                    offending.Add($"{controllerType.Name} depends on {param.ParameterType.Name}");
            }
        }

        Assert.True(offending.Count == 0,
            "AdminImportPackageController must not depend on the STT ledger directly — only " +
            "ImportPackageProcessingService orchestrates STT calls: " + string.Join(", ", offending));
    }

    /// <summary>Workstream B12: "use decimal types for monetary values... do not persist monetary
    /// values as double or float." Checks every property on the Phase 4.4 cost-bearing entities
    /// whose name suggests a monetary/price value.</summary>
    [Fact]
    public void Monetary_properties_on_import_cost_entities_use_decimal_not_double_or_float()
    {
        var monetaryNameFragments = new[] { "Cost", "Price", "Ceiling" };
        var typesToCheck = new[]
        {
            typeof(LinguaCoach.Domain.Entities.ImportPackage),
            typeof(LinguaCoach.Domain.Entities.ImportProfile),
            typeof(LinguaCoach.Domain.Entities.ImportSttOperation),
        };

        var offending = new List<string>();
        foreach (var type in typesToCheck)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!monetaryNameFragments.Any(f => prop.Name.Contains(f, StringComparison.Ordinal))) continue;
                var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (underlying == typeof(double) || underlying == typeof(float))
                    offending.Add($"{type.Name}.{prop.Name} is {underlying.Name}");
            }
        }

        Assert.True(offending.Count == 0,
            "Found a monetary property using double/float instead of decimal: " + string.Join(", ", offending));
    }

    /// <summary>Workstream B11: exactly one ledger row may ever exist per logical STT operation —
    /// guards against the unique index being accidentally dropped from the EF configuration.
    /// </summary>
    [Fact]
    public void Stt_operation_logical_key_has_a_unique_index_configured()
    {
        using var db = new LinguaCoach.Persistence.LinguaCoachDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<LinguaCoach.Persistence.LinguaCoachDbContext>()
                .UseSqlite("DataSource=:memory:").Options);

        var entityType = db.Model.FindEntityType(typeof(LinguaCoach.Domain.Entities.ImportSttOperation));
        Assert.NotNull(entityType);

        var hasUniqueIndexOnLogicalKey = entityType!.GetIndexes().Any(i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(LinguaCoach.Domain.Entities.ImportSttOperation.LogicalOperationKey));

        Assert.True(hasUniqueIndexOnLogicalKey,
            "ImportSttOperation.LogicalOperationKey must have a unique index — this is the DB-level dedup guarantee.");
    }
}
