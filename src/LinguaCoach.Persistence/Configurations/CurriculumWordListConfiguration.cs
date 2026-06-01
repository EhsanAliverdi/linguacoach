using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CurriculumWordListConfiguration : IEntityTypeConfiguration<CurriculumWordList>
{
    public void Configure(EntityTypeBuilder<CurriculumWordList> builder)
    {
        builder.ToTable("curriculum_word_lists");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.CareerProfileId).HasColumnName("career_profile_id").IsRequired();
        builder.Property(e => e.LanguagePairId).HasColumnName("language_pair_id").IsRequired();
        builder.Property(e => e.Word).HasColumnName("word").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Definition).HasColumnName("definition").IsRequired();
        builder.Property(e => e.ExampleSentence).HasColumnName("example_sentence").IsRequired();
        builder.Property(e => e.Priority).HasColumnName("priority").IsRequired();
        builder.Property(e => e.Tags).HasColumnName("tags").HasMaxLength(500).IsRequired();

        builder.HasIndex(e => new { e.CareerProfileId, e.LanguagePairId, e.Priority })
            .HasDatabaseName("ix_curriculum_word_lists_career_lang_priority");

        builder.HasOne<Domain.Entities.CareerProfile>()
            .WithMany()
            .HasForeignKey(e => e.CareerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.LanguagePair>()
            .WithMany()
            .HasForeignKey(e => e.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
