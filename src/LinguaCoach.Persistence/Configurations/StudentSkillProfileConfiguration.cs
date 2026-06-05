using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentSkillProfileConfiguration : IEntityTypeConfiguration<StudentSkillProfile>
{
    public void Configure(EntityTypeBuilder<StudentSkillProfile> builder)
    {
        builder.ToTable("student_skill_profiles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");
        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.SkillKey).HasColumnName("skill_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SkillLabel).HasColumnName("skill_label").HasMaxLength(200).IsRequired();
        builder.Property(e => e.IsWeak).HasColumnName("is_weak").IsRequired();
        builder.Property(e => e.LastUpdatedUtc).HasColumnName("last_updated_utc").IsRequired();

        builder.HasIndex(e => new { e.StudentProfileId, e.SkillKey })
            .IsUnique()
            .HasDatabaseName("ix_student_skill_profiles_student_key");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
