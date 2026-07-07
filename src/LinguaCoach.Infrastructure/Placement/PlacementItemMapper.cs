using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.Placement;

internal static class PlacementItemMapper
{
    public static AdminPlacementItemDto ToDto(PlacementItemDefinition item) => new(
        item.Id, item.Skill, item.CefrLevel, item.ItemOrder, item.IsEnabled,
        item.FormIoSchemaJson, item.ScoringRulesJson, item.ScoringRulesVersion, item.RendererKind.ToString(),
        PlacementItemSchemaLabel.ExtractLabel(item.FormIoSchemaJson), item.AuthoringSchemaJson,
        item.DifficultyBand, item.DiscriminationIndex, item.CalibrationSampleSize, item.EvidenceWeight,
        item.ReviewStatus.ToString(), item.ItemVersion);
}
