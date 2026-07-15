using System.Text.Json;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportProfileConfiguration : IEntityTypeConfiguration<ImportProfile>
{
    public void Configure(EntityTypeBuilder<ImportProfile> builder)
    {
        builder.ToTable("import_profiles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ImportPackageId).HasColumnName("import_package_id").IsRequired();
        builder.Property(e => e.Version).HasColumnName("version").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ProfileJson).HasColumnName("profile_json").IsRequired();

        builder.Property(e => e.AiProviderName).HasColumnName("ai_provider_name").HasMaxLength(100);
        builder.Property(e => e.AiModelName).HasColumnName("ai_model_name").HasMaxLength(100);

        builder.Property(e => e.SampleAssetIds)
            .HasColumnName("sample_asset_ids_json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<Guid>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.ToList()));

        builder.Property(e => e.EstimatedCandidateCount).HasColumnName("estimated_candidate_count").IsRequired().HasDefaultValue(0);

        builder.Property(e => e.EstimatedCostExpected).HasColumnName("estimated_cost_expected").HasPrecision(12, 4).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.EstimatedCostMin).HasColumnName("estimated_cost_min").HasPrecision(12, 4).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.EstimatedCostMax).HasColumnName("estimated_cost_max").HasPrecision(12, 4).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired().HasDefaultValue("USD");
        builder.Property(e => e.PlanEstimateJson).HasColumnName("plan_estimate_json");
        builder.Property(e => e.PricingSnapshotJson).HasColumnName("pricing_snapshot_json");
        builder.Property(e => e.ApprovedCostCeiling).HasColumnName("approved_cost_ceiling").HasPrecision(12, 4);

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(e => e.ApprovedAtUtc).HasColumnName("approved_at_utc");
        builder.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(e => e.RejectedAtUtc).HasColumnName("rejected_at_utc");
        builder.Property(e => e.RejectedByUserId).HasColumnName("rejected_by_user_id");
        builder.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(2000);
        builder.Property(e => e.ChangeReason).HasColumnName("change_reason").HasMaxLength(2000);
        builder.Property(e => e.PauseReason).HasColumnName("pause_reason").HasMaxLength(2000);

        builder.HasOne<ImportPackage>()
            .WithMany()
            .HasForeignKey(e => e.ImportPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ImportPackageId).HasDatabaseName("ix_import_profiles_package");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_import_profiles_status");
    }
}
