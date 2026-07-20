using LinguaCoach.Application.Composer;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Adaptive Curriculum Sprint 5 — replaces the real AI-backed <see cref="ICurriculumComposerService"/>
/// for the integration test host, mirroring <see cref="FakeAudioDurationProbe"/>'s convention (no
/// real external AI provider/credentials in the test/CI environment). Pass-through by default
/// (candidates in given order, capped to MaxResults) so Today/Practice Gym selection tests exercise
/// real eligibility/CEFR/recency filtering end-to-end without a live AI call.
/// </summary>
public sealed class FakeCurriculumComposerService : ICurriculumComposerService
{
    public Task<ComposerRankingResult> RankCandidatesAsync(ComposerRankingRequest request, CancellationToken ct = default)
    {
        if (request.Candidates.Count == 0)
            return Task.FromResult(new ComposerRankingResult(false, [], null, "No eligible candidates."));

        var ranked = request.Candidates.Select(c => c.ModuleId).Take(request.MaxResults).ToList();
        return Task.FromResult(new ComposerRankingResult(true, ranked, "fake composer selection (integration test)", null));
    }
}
