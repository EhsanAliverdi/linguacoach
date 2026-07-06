using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentFlowSubmissionConfiguration : IEntityTypeConfiguration<StudentFlowSubmission>
{
    public void Configure(EntityTypeBuilder<StudentFlowSubmission> builder)
    {
        builder.ToTable("student_flow_submissions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(s => s.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(s => s.FlowKind).HasColumnName("flow_kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.TemplateVersionId).HasColumnName("template_version_id").IsRequired();
        builder.Property(s => s.SubmissionJson).HasColumnName("submission_json").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.NormalizedAnswersJson).HasColumnName("normalized_answers_json").HasColumnType("jsonb");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(s => s.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(s => s.EvaluatedAt).HasColumnName("evaluated_at");

        // One active (non-evaluated) submission per student+flow is enforced in application logic,
        // not a DB constraint, since historical submissions must remain queryable.
        builder.HasIndex(s => new { s.StudentId, s.FlowKind })
            .HasDatabaseName("ix_student_flow_submissions_student_flow");
    }
}
