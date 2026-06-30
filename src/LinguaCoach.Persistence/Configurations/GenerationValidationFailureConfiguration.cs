using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class GenerationValidationFailureConfiguration : IEntityTypeConfiguration<GenerationValidationFailure>
{
    public void Configure(EntityTypeBuilder<GenerationValidationFailure> builder)
    {
        builder.ToTable("generation_validation_failures");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(f => f.PatternKey).HasColumnName("pattern_key").HasMaxLength(100).IsRequired(false);
        builder.Property(f => f.ActivityTypeName).HasColumnName("activity_type_name").HasMaxLength(100).IsRequired();
        builder.Property(f => f.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired(false);
        builder.Property(f => f.ObjectiveKey).HasColumnName("objective_key").HasMaxLength(200).IsRequired(false);
        builder.Property(f => f.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired(false);
        builder.Property(f => f.ModelName).HasColumnName("model_name").HasMaxLength(100).IsRequired(false);
        builder.Property(f => f.ValidationErrors).HasColumnName("validation_errors").HasMaxLength(2000).IsRequired();
        builder.Property(f => f.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(f => f.CorrelationId).HasColumnName("correlation_id").HasMaxLength(50).IsRequired(false);
        builder.Property(f => f.StudentProfileId).HasColumnName("student_profile_id").IsRequired(false);

        builder.HasIndex(f => f.PatternKey).HasDatabaseName("ix_gen_val_failures_pattern_key");
        builder.HasIndex(f => f.CreatedAt).HasDatabaseName("ix_gen_val_failures_created_at");
        builder.HasIndex(f => f.CefrLevel).HasDatabaseName("ix_gen_val_failures_cefr_level");
    }
}
