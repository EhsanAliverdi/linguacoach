namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Test factory for the live adaptive placement engine. The adaptive engine
/// (IPlacementAssessmentService/PlacementScoringService) is fully deterministic and has no AI
/// dependency of its own — this factory only needs ActivityTestFactory's fake-AI wiring and
/// onboarded-student helper, which every placement test relies on for setup.
/// </summary>
public sealed class PlacementTestFactory : ActivityTestFactory
{
}
