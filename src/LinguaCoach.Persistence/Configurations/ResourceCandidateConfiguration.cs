using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ResourceCandidateConfiguration : IEntityTypeConfiguration<ResourceCandidate>
{
    public void Configure(EntityTypeBuilder<ResourceCandidate> builder)
    {
        builder.ToTable("resource_candidates");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.Property(e => e.ResourceRawRecordId).HasColumnName("resource_raw_record_id").IsRequired();
        builder.Property(e => e.CandidateType).HasColumnName("candidate_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.CanonicalText).HasColumnName("canonical_text").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.NormalizedJson).HasColumnName("normalized_json").IsRequired();
        builder.Property(e => e.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();

        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10);
        builder.Property(e => e.CefrConfidence).HasColumnName("cefr_confidence");
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(100);
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band");

        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").HasDefaultValue("[]");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").HasDefaultValue("[]");
        builder.Property(e => e.GrammarTagsJson).HasColumnName("grammar_tags_json");
        builder.Property(e => e.VocabularyTagsJson).HasColumnName("vocabulary_tags_json");
        builder.Property(e => e.PronunciationTagsJson).HasColumnName("pronunciation_tags_json");
        builder.Property(e => e.ActivitySuitabilityTagsJson).HasColumnName("activity_suitability_tags_json");
        builder.Property(e => e.SafetyTagsJson).HasColumnName("safety_tags_json");
        builder.Property(e => e.LicenseTagsJson).HasColumnName("license_tags_json");
        builder.Property(e => e.QualityScore).HasColumnName("quality_score");

        builder.Property(e => e.SearchText).HasColumnName("search_text").IsRequired();
        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint").HasMaxLength(128).IsRequired();
        builder.Property(e => e.AiAnalysisJson).HasColumnName("ai_analysis_json");

        builder.Property(e => e.ValidationStatus).HasColumnName("validation_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.RejectReason).HasColumnName("reject_reason");
        builder.Property(e => e.AdminNotes).HasColumnName("admin_notes");

        builder.Property(e => e.IsPublished).HasColumnName("is_published").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(e => e.PublishedEntityType).HasColumnName("published_entity_type").HasMaxLength(64);
        builder.Property(e => e.PublishedEntityId).HasColumnName("published_entity_id");
        builder.Property(e => e.PublishedByUserId).HasColumnName("published_by_user_id");

        builder.Property(e => e.AudioStorageKey).HasColumnName("audio_storage_key").HasMaxLength(500);
        builder.Property(e => e.AudioContentType).HasColumnName("audio_content_type").HasMaxLength(100);

        builder.Property(e => e.TranscriptOrigin).HasColumnName("transcript_origin").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.TranscriptConfidence).HasColumnName("transcript_confidence");
        builder.Property(e => e.SttProviderName).HasColumnName("stt_provider_name").HasMaxLength(100);
        builder.Property(e => e.SttModelName).HasColumnName("stt_model_name").HasMaxLength(100);
        builder.Property(e => e.MetadataProvenanceJson).HasColumnName("metadata_provenance_json");

        builder.HasOne<ResourceRawRecord>()
            .WithMany()
            .HasForeignKey(e => e.ResourceRawRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ResourceRawRecordId).HasDatabaseName("ix_resource_candidates_raw_record");
        builder.HasIndex(e => e.ContentFingerprint).HasDatabaseName("ix_resource_candidates_fingerprint");
        builder.HasIndex(e => e.CandidateType).HasDatabaseName("ix_resource_candidates_type");
        builder.HasIndex(e => e.ValidationStatus).HasDatabaseName("ix_resource_candidates_validation_status");
        builder.HasIndex(e => e.ReviewStatus).HasDatabaseName("ix_resource_candidates_review_status");
        builder.HasIndex(e => e.IsPublished).HasDatabaseName("ix_resource_candidates_is_published");
    }
}
