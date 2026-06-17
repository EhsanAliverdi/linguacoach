using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class StudentPolicyAssignmentConfiguration : IEntityTypeConfiguration<StudentPolicyAssignment>
{
    public void Configure(EntityTypeBuilder<StudentPolicyAssignment> builder)
    {
        builder.ToTable("StudentPolicyAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasOne(x => x.StudentProfile).WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.StudentProfileId, x.IsActive });
    }
}
