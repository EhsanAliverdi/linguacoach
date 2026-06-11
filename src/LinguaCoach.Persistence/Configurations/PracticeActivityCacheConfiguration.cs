using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PracticeActivityCacheConfiguration : IEntityTypeConfiguration<PracticeActivityCache>
{
    public void Configure(EntityTypeBuilder<PracticeActivityCache> builder)
    {
        builder.ToTable("practice_activity_cache");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.DomainComplexity).HasColumnName("domain_complexity").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SkillFocus).HasColumnName("skill_focus").HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint").HasMaxLength(128).IsRequired();
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired(false);
        builder.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired(false);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.HasIndex(e => new { e.StudentProfileId, e.PatternKey, e.Status })
            .HasDatabaseName("ix_practice_cache_student_pattern_status");

        // Duplicate prevention (T-idempotency: student + pattern + cefr + domain + fingerprint).
        builder.HasIndex(e => new
            {
                e.StudentProfileId,
                e.PatternKey,
                e.CefrLevel,
                e.DomainComplexity,
                e.ContentFingerprint
            })
            .HasDatabaseName("ux_practice_cache_fingerprint")
            .IsUnique();
    }
}
