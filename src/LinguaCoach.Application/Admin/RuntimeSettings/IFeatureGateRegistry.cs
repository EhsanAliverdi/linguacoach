namespace LinguaCoach.Application.Admin.RuntimeSettings;

/// <summary>Static, code-defined catalogue of feature-gate groups. Holds no live values.</summary>
public interface IFeatureGateRegistry
{
    IReadOnlyList<FeatureGateGroupDefinition> GetAllGroups();
    FeatureGateGroupDefinition? GetGroup(string groupKey);
}
