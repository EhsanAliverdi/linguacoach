using LinguaCoach.Application.Admin.RuntimeSettings;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class FeatureGateRegistryService : IFeatureGateRegistry
{
    public IReadOnlyList<FeatureGateGroupDefinition> GetAllGroups() => FeatureGateDefinitions.All;

    public FeatureGateGroupDefinition? GetGroup(string groupKey) =>
        FeatureGateDefinitions.All.FirstOrDefault(g => g.GroupKey == groupKey);
}
