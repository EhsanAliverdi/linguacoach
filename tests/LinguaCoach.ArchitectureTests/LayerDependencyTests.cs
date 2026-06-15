using NetArchTest.Rules;

namespace LinguaCoach.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture layer boundary rules documented in AGENTS.md:
/// Domain must not depend on EF Core, ASP.NET Identity, Infrastructure, or API.
/// Application defines interfaces and use cases without depending on Infrastructure/API.
/// </summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "LinguaCoach.Domain";
    private const string ApplicationNamespace = "LinguaCoach.Application";
    private const string InfrastructureNamespace = "LinguaCoach.Infrastructure";
    private const string PersistenceNamespace = "LinguaCoach.Persistence";
    private const string ApiNamespace = "LinguaCoach.Api";

    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure_Persistence_Or_Api()
    {
        var result = Types.InAssembly(typeof(LinguaCoach.Domain.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, PersistenceNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureSummary(result));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_EfCore_Or_AspNetIdentity()
    {
        var result = Types.InAssembly(typeof(LinguaCoach.Domain.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Identity")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureSummary(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Persistence_Or_Api()
    {
        var result = Types.InAssembly(typeof(LinguaCoach.Application.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, PersistenceNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureSummary(result));
    }

    private static string FailureSummary(TestResult result) =>
        result.FailingTypeNames is null
            ? "Architecture rule failed."
            : "Architecture rule failed for: " + string.Join(", ", result.FailingTypeNames);
}
