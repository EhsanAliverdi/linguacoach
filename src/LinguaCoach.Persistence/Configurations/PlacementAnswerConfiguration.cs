using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PlacementAnswerConfiguration : IEntityTypeConfiguration<PlacementAnswer>
{
    public void Configure(EntityTypeBuilder<PlacementAnswer> builder)
    {
        builder.ToTable("placement_answers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(e => e.PlacementAssessmentId).HasColumnName("placement_assessment_id").IsRequired();
        builder.Property(e => e.SectionKey).HasColumnName("section_key").HasMaxLength(50).IsRequired();
        builder.Property(e => e.QuestionKey).HasColumnName("question_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ResponseText).HasColumnName("response_text");
        builder.Property(e => e.SelectedOption).HasColumnName("selected_option").HasMaxLength(500);
        builder.Property(e => e.Score).HasColumnName("score");

        builder.HasIndex(e => e.PlacementAssessmentId)
            .HasDatabaseName("ix_placement_answers_placement_assessment_id");
    }
}
