using LinguaCoach.IntegrationTests.Api;

namespace LinguaCoach.IntegrationTests.UsageGovernance;

/// <summary>
/// Dedicated factory for usage governance integration tests.
/// Subclassing ensures xUnit creates a fresh WebApplicationFactory instance
/// with its own SQLite connection, independent of other test class fixtures.
/// </summary>
public sealed class UsageGovernanceTestFactory : ApiTestFactory
{
}
