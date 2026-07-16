using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportCostCeilingAmendmentConfiguration : IEntityTypeConfiguration<ImportCostCeilingAmendment>
{
    public void Configure(EntityTypeBuilder<ImportCostCeilingAmendment> builder)
    {
        builder.ToTable("import_cost_ceiling_amendments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ImportPackageId).HasColumnName("import_package_id").IsRequired();
        builder.Property(e => e.ImportProfileId).HasColumnName("import_profile_id").IsRequired();
        builder.Property(e => e.PreviousCeiling).HasColumnName("previous_ceiling").HasPrecision(12, 4).IsRequired();
        builder.Property(e => e.NewCeiling).HasColumnName("new_ceiling").HasPrecision(12, 4).IsRequired();
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired().HasDefaultValue("USD");
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.AdministratorUserId).HasColumnName("administrator_user_id");
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<ImportPackage>()
            .WithMany()
            .HasForeignKey(e => e.ImportPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ImportProfile>()
            .WithMany()
            .HasForeignKey(e => e.ImportProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ImportPackageId).HasDatabaseName("ix_import_cost_ceiling_amendments_package");
        builder.HasIndex(e => e.ImportProfileId).HasDatabaseName("ix_import_cost_ceiling_amendments_profile");
    }
}
