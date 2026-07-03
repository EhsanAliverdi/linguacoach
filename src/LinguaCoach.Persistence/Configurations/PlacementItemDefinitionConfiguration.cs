using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PlacementItemDefinitionConfiguration : IEntityTypeConfiguration<PlacementItemDefinition>
{
    public void Configure(EntityTypeBuilder<PlacementItemDefinition> builder)
    {
        builder.ToTable("placement_item_definitions");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(i => i.Skill).HasColumnName("skill").IsRequired().HasMaxLength(50);
        builder.Property(i => i.CefrLevel).HasColumnName("cefr_level").IsRequired().HasMaxLength(10);
        builder.Property(i => i.ItemType).HasColumnName("item_type").IsRequired().HasMaxLength(50);
        builder.Property(i => i.Prompt).HasColumnName("prompt").IsRequired().HasMaxLength(2000);
        builder.Property(i => i.CorrectAnswer).HasColumnName("correct_answer").IsRequired().HasMaxLength(500);
        builder.Property(i => i.ReadingPassage).HasColumnName("reading_passage").HasMaxLength(4000);
        builder.Property(i => i.ListeningAudioScript).HasColumnName("listening_audio_script").HasMaxLength(4000);
        builder.Property(i => i.ItemOrder).HasColumnName("item_order").IsRequired();
        builder.Property(i => i.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(i => i.ContentJson).HasColumnName("content_json").HasColumnType("jsonb");
        builder.Ignore(i => i.Content);

        // Prompt uniqueness is the de-facto item identity used by the adaptive selection
        // logic's "used prompts" dedup — enforce it at the DB level too.
        builder.HasIndex(i => i.Prompt)
            .IsUnique()
            .HasDatabaseName("ix_placement_item_definitions_prompt");

        builder.HasIndex(i => new { i.Skill, i.CefrLevel, i.IsEnabled })
            .HasDatabaseName("ix_placement_item_definitions_skill_level_enabled");
    }
}
