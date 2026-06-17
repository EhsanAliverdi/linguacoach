using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class StudentUsageDailyConfiguration : IEntityTypeConfiguration<StudentUsageDaily>
{
    public void Configure(EntityTypeBuilder<StudentUsageDaily> builder)
    {
        builder.ToTable("StudentUsageDaily");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TotalCost).HasColumnType("decimal(18,6)");
        builder.Property(x => x.LiveAiMinutes).HasColumnType("decimal(10,3)");
        builder.Property(x => x.SttMinutes).HasColumnType("decimal(10,3)");
        builder.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.StudentProfileId, x.Date }).IsUnique();
    }
}
