using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E4 — publishes an approved, validated ResourceCandidate into its target Cefr* bank
/// table. SQLite in-memory, matching ResourceCandidateValidationServiceTests'/
/// ResourceCandidatePreviewServiceTests' convention.
/// </summary>
public sealed class ResourceCandidatePublishServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceCandidatePublishService _sut;
    private readonly ActivityContentFingerprintService _fingerprint = new();

    public ResourceCandidatePublishServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource(
        bool approved = true, bool allowsStudentDisplay = true, bool allowsCommercialUse = true)
    {
        var source = new CefrResourceSource(
            $"Test Source {Guid.NewGuid():N}", "CC-BY-4.0",
            allowsStudentDisplay: allowsStudentDisplay, allowsCommercialUse: allowsCommercialUse,
            attributionText: "Test Attribution");
        if (approved) source.ApproveForImport("cleared for test");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    /// <summary>Phase 4.2 — every publishable candidate must trace back to an ImportPackage with
    /// an approved Import Execution Plan; this helper attaches that provenance by default so the
    /// existing publish-gate tests below keep exercising their own specific gate, not this one.
    /// See the dedicated provenance tests near the bottom of this file.</summary>
    private Guid SeedApprovedPackage(CefrResourceSource source)
    {
        var package = new ImportPackage(source.Id, "test-package", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        _db.SaveChanges();

        var plan = new ImportProfile(
            package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow);
        plan.SubmitForApproval();
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        _db.ImportProfiles.Add(plan);
        package.ApproveProfile(plan.Id);
        _db.SaveChanges();

        return package.Id;
    }

    private (ResourceImportRun Run, ResourceRawRecord Raw) SeedRunAndRaw(CefrResourceSource source, string rawJson)
    {
        var run = new ResourceImportRun(
            source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow,
            importPackageId: SeedApprovedPackage(source));
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, $"rawhash-{Guid.NewGuid():N}", "en", "row", rawJson: rawJson);
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();

        return (run, raw);
    }

    private string Fingerprint(string canonicalText, string normalizedJson) =>
        _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(normalizedJson, ActivityContentShape.Unknown, null, canonicalText));

    private ResourceCandidate SeedCandidate(
        ResourceRawRecord raw, ResourceCandidateType type, string canonicalText, string normalizedJson,
        string languageCode = "en")
    {
        var candidate = new ResourceCandidate(
            raw.Id, type, canonicalText, normalizedJson, languageCode,
            canonicalText, Fingerprint(canonicalText, normalizedJson), ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();
        return candidate;
    }

    /// <summary>Brings a freshly-seeded candidate to the "ready to publish" state: CEFR level set,
    /// ValidationStatus=Passed, ReviewStatus=Approved.</summary>
    private void MakePublishReady(ResourceCandidate candidate, string cefrLevel = "A1", string? primarySkill = "vocabulary")
    {
        candidate.ApplyAnalysis(
            """{"cefrLevel":"A1"}""", cefrLevel, 0.95, primarySkill, null, 1,
            "[]", "[]", null, null, null, null, null, 0.9, candidate.CanonicalText);
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Passed, """{"errors":[],"warnings":[]}""");
        candidate.Approve("looks good");
        _db.SaveChanges();
    }

    // ── Gate tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cannot_publish_a_Failed_candidate_even_if_approved()
    {
        // Phase K2 semantics: Failed (hard validation error) always blocks publish, regardless of
        // admin approval — this is the "true hard block" case Approve & Publish must refuse.
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        candidate.ApplyAnalysis(
            """{}""", "A1", 0.95, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, "hello");
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Failed, """{"errors":["Safety concern(s) reported: flagged"],"warnings":[]}""");
        candidate.Approve();
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ValidationStatus"));
        result.Errors.Should().Contain(e => e.Contains("Blocking error(s)") && e.Contains("Safety concern"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }

    [Fact]
    public async Task Cannot_publish_a_never_validated_Pending_candidate_even_if_approved()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        // Constructed directly at Pending (SeedCandidate's helper always starts at NeedsReview,
        // matching real import behavior — Pending only exists as a defensive gate case).
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", Fingerprint("hello", """{"word":"hello"}"""), ResourceCandidateValidationStatus.Pending);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();
        candidate.ApplyAnalysis(
            """{}""", "A1", 0.95, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, "hello");
        candidate.Approve();
        _db.SaveChanges();
        // ValidationStatus is still Pending — never validated at all.

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ValidationStatus"));
        result.Errors.Should().Contain(e => e.Contains("has not been validated yet"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }

    [Fact]
    public async Task NeedsReview_candidate_with_only_advisory_warnings_publishes_successfully_once_approved()
    {
        // Phase K2 — root-cause regression test for the reported bug: NeedsReview must be
        // publishable after admin approval (warnings are advisory, not a hard block). Before the
        // fix, PublishAsync's gate required ValidationStatus == Passed exactly, so an
        // Approve & Publish click on a warning-only candidate always failed with
        // "ValidationStatus must be Passed to publish; current: NeedsReview."
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        candidate.ApplyAnalysis(
            """{}""", "A1", 0.95, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, "hello");
        candidate.ApplyValidation(
            ResourceCandidateValidationStatus.NeedsReview,
            """{"errors":[],"warnings":["Duplicate: another candidate with the same content fingerprint exists elsewhere from the same source."]}""");
        candidate.Approve("advisory warning only, override approved");
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrVocabularyEntry");
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(1);
    }

    [Fact]
    public async Task Cannot_publish_before_review_approved()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        candidate.ApplyAnalysis(
            """{}""", "A1", 0.95, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, "hello");
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Passed, """{"errors":[],"warnings":[]}""");
        _db.SaveChanges();
        // ReviewStatus is PendingReview (auto-promoted by ApplyValidation), not Approved.

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ReviewStatus"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }

    [Fact]
    public async Task Rejected_candidate_cannot_publish()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);
        candidate.Reject("not good enough");
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ReviewStatus"));
    }

    [Fact]
    public async Task Source_revoked_after_validation_blocks_publish()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);

        // Revoke approval after validation/approval already happened.
        source.RevokeApproval("license terms changed");
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("no longer approved"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }

    [Fact]
    public async Task Missing_student_display_or_commercial_use_hard_blocks_publish()
    {
        var source = SeedSource(allowsStudentDisplay: false, allowsCommercialUse: false);
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("student display"));
        result.Errors.Should().Contain(e => e.Contains("commercial use"));
    }

    [Fact]
    public async Task English_only_gate_is_rechecked_at_publish_time()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"bonjour"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "bonjour", """{"word":"bonjour"}""", languageCode: "fr");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not English"));
    }

    [Fact]
    public async Task Persian_script_candidate_cannot_publish_even_if_marked_approved()
    {
        // Defense-in-depth: LanguageCode says "en" but the actual content is Persian-script — the
        // script heuristic must still catch this at publish time, not just trust the stored
        // LanguageCode/ValidationStatus.
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"سلام"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "سلام", """{"word":"سلام"}""", languageCode: "en");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("English-only script check"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }

    // ── Successful publish per candidate type ───────────────────────────────────

    [Fact]
    public async Task Publishing_vocabulary_candidate_creates_exactly_one_row_with_mapped_fields()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello","pos":"interjection","definition":"a greeting"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.VocabularyEntry, "hello",
            """{"word":"hello","pos":"interjection","definition":"a greeting"}""");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrVocabularyEntry");
        result.PublishedEntityId.Should().NotBeNull();

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Vocabulary).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<VocabularyContent>(rows[0].ContentJson);
        content.Word.Should().Be("hello");
        rows[0].CefrLevel.Should().Be("A1");
        content.PartOfSpeech.Should().Be("interjection");
        rows[0].SourceId.Should().Be(source.Id);

        var reloadedCandidate = await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id);
        reloadedCandidate.IsPublished.Should().BeTrue();
        reloadedCandidate.PublishedAtUtc.Should().NotBeNull();
        reloadedCandidate.PublishedEntityType.Should().Be("CefrVocabularyEntry");
        reloadedCandidate.PublishedEntityId.Should().Be(rows[0].Id);
    }

    [Fact]
    public async Task Publishing_grammar_candidate_creates_exactly_one_row()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"grammarKey":"present simple","explanation":"habitual actions"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.GrammarProfileEntry, "present simple",
            """{"grammarKey":"present simple","explanation":"habitual actions"}""");
        MakePublishReady(candidate, primarySkill: "grammar");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrGrammarProfileEntry");

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Grammar).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<GrammarContent>(rows[0].ContentJson);
        content.GrammarPoint.Should().Be("present simple");
        content.Description.Should().Be("habitual actions");
    }

    [Fact]
    public async Task Short_reading_passage_publishes_as_reading_reference()
    {
        var source = SeedSource();
        var shortPassage = "A short excerpt about daily routines and morning habits.";
        var (_, raw) = SeedRunAndRaw(source, $$"""{"title":"Daily routines","passage":"{{shortPassage}}"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.ReadingPassage, "Daily routines",
            $$"""{"title":"Daily routines","passage":"{{shortPassage}}"}""");
        MakePublishReady(candidate, primarySkill: "reading");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrReadingReference");

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.ReadingReference).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(rows[0].ContentJson);
        content.ReferenceExcerpt.Should().Be(shortPassage);
    }

    [Fact]
    public async Task Long_reading_passage_publishes_to_CefrReadingPassage_not_truncated_into_CefrReadingReference()
    {
        // Phase E7: a full-length passage is no longer blocked at publish time — it routes to the
        // new CefrReadingPassage bank instead of being silently truncated into
        // CefrReadingReference (which stays short-excerpt-only).
        var source = SeedSource();
        var longPassage = string.Concat(Enumerable.Repeat("This is a sentence about grammar and vocabulary practice. ", 20)); // > 500 chars
        longPassage.Length.Should().BeGreaterThan(ResourceCandidatePublishService.MaxReadingExcerptLength);

        var (_, raw) = SeedRunAndRaw(source, $$"""{"title":"Long passage","passage":"{{longPassage}}"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.ReadingPassage, "Long passage",
            $$"""{"title":"Long passage","passage":"{{longPassage}}"}""");
        MakePublishReady(candidate, primarySkill: "reading");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrReadingPassage");
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingReference)).Should().Be(0);

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.ReadingPassage).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<ReadingPassageContent>(rows[0].ContentJson);
        content.Title.Should().Be("Long passage");
        content.PassageText.Should().Be(longPassage.Trim());
        rows[0].CefrLevel.Should().Be("A1");
        content.PrimarySkill.Should().Be("reading");
        content.WordCount.Should().BeGreaterThan(0);
        rows[0].SourceId.Should().Be(source.Id);
        content.AttributionText.Should().Be("Test Attribution");
    }

    [Fact]
    public async Task Full_reading_passage_without_a_title_field_is_blocked_with_a_clear_error()
    {
        var source = SeedSource();
        var longPassage = string.Concat(Enumerable.Repeat("This is a sentence about grammar and vocabulary practice. ", 20));
        var (_, raw) = SeedRunAndRaw(source, $$"""{"passage":"{{longPassage}}"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.ReadingPassage, longPassage,
            $$"""{"passage":"{{longPassage}}"}""");
        MakePublishReady(candidate, primarySkill: "reading");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'title' field is required"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingPassage)).Should().Be(0);
    }

    [Fact]
    public async Task Republishing_an_already_published_full_reading_passage_is_idempotent()
    {
        var source = SeedSource();
        var longPassage = string.Concat(Enumerable.Repeat("This is a sentence about grammar and vocabulary practice. ", 20));
        var (_, raw) = SeedRunAndRaw(source, $$"""{"title":"Long passage","passage":"{{longPassage}}"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.ReadingPassage, "Long passage",
            $$"""{"title":"Long passage","passage":"{{longPassage}}"}""");
        MakePublishReady(candidate, primarySkill: "reading");

        var first = await _sut.PublishAsync(candidate.Id, null);
        var second = await _sut.PublishAsync(candidate.Id, null);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.PublishedEntityId.Should().Be(first.PublishedEntityId);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingPassage)).Should().Be(1);
    }

    [Fact]
    public async Task Publishing_writing_prompt_candidate_creates_exactly_one_row_with_mapped_fields()
    {
        var source = SeedSource();
        var rawJson = """{"title":"Email reply","prompt":"Write a reply to your manager about a scheduling conflict.","genre":"Email","minWords":"80"}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.WritingPrompt, "Email reply", rawJson);
        MakePublishReady(candidate, primarySkill: "writing");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrWritingPrompt");

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Writing).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<WritingPromptContent>(rows[0].ContentJson);
        content.Title.Should().Be("Email reply");
        content.PromptText.Should().Be("Write a reply to your manager about a scheduling conflict.");
        content.Genre.Should().Be("Email");
        content.SuggestedMinWords.Should().Be(80);
        rows[0].CefrLevel.Should().Be("A1");
        rows[0].SourceId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Writing_prompt_without_a_title_derives_one_from_the_prompt_text()
    {
        var source = SeedSource();
        var rawJson = """{"prompt":"Describe your typical morning routine."}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.WritingPrompt, "Describe your typical morning routine.", rawJson);
        MakePublishReady(candidate, primarySkill: "writing");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Writing).ToListAsync();
        var content = ResourceBankItemContent.Deserialize<WritingPromptContent>(rows[0].ContentJson);
        content.Title.Should().Be("Describe your typical morning routine.");
    }

    [Fact]
    public async Task Listening_candidate_without_uploaded_audio_cannot_publish()
    {
        var source = SeedSource();
        var rawJson = """{"title":"Morning News","transcript":"Good morning and welcome to the daily news."}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.ListeningPassage, "Morning News", rawJson);
        MakePublishReady(candidate, primarySkill: "listening");
        // No AttachAudio call — this candidate has no uploaded audio file.

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("audio file is required"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Listening)).Should().Be(0);
    }

    [Fact]
    public async Task Publishing_listening_candidate_with_audio_creates_exactly_one_row_with_mapped_fields()
    {
        var source = SeedSource();
        var rawJson = """{"title":"Morning News","transcript":"Good morning and welcome to the daily news."}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.ListeningPassage, "Morning News", rawJson);
        MakePublishReady(candidate, primarySkill: "listening");
        candidate.AttachAudio("resource-import-audio/abc123.mp3", "audio/mpeg");
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrListeningPassage");

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Listening).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<ListeningPassageContent>(rows[0].ContentJson);
        content.Title.Should().Be("Morning News");
        content.Transcript.Should().Be("Good morning and welcome to the daily news.");
        content.AudioStorageKey.Should().Be("resource-import-audio/abc123.mp3");
        content.AudioContentType.Should().Be("audio/mpeg");
        rows[0].CefrLevel.Should().Be("A1");
        rows[0].SourceId.Should().Be(source.Id);
    }

    /// <summary>Phase 4.6 — a candidate's real measured audio duration (threaded from its linked
    /// ImportAsset by ImportPackageProcessingService) must be preserved verbatim into
    /// ListeningPassageContent at publish time — never dropped, never re-derived.</summary>
    [Fact]
    public async Task Publishing_listening_candidate_preserves_audio_duration_into_ListeningPassageContent()
    {
        var source = SeedSource();
        var rawJson = """{"title":"Morning News","transcript":"Good morning and welcome to the daily news."}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.ListeningPassage, "Morning News", rawJson);
        MakePublishReady(candidate, primarySkill: "listening");
        candidate.AttachAudio("resource-import-audio/abc123.mp3", "audio/mpeg");
        candidate.SetAudioDuration(97.5m);
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Listening).ToListAsync();
        var content = ResourceBankItemContent.Deserialize<ListeningPassageContent>(rows[0].ContentJson);
        content.AudioDurationSeconds.Should().Be(97.5m);
    }

    /// <summary>Phase 4.6 — a candidate whose audio duration was never measured (e.g. a manual
    /// upload with no linked ImportAsset) must still publish — a null duration is a valid "not
    /// known" state, never a publish blocker.</summary>
    [Fact]
    public async Task Publishing_listening_candidate_with_unknown_audio_duration_still_publishes()
    {
        var source = SeedSource();
        var rawJson = """{"title":"Morning News","transcript":"Good morning and welcome to the daily news."}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.ListeningPassage, "Morning News", rawJson);
        MakePublishReady(candidate, primarySkill: "listening");
        candidate.AttachAudio("resource-import-audio/abc123.mp3", "audio/mpeg");
        _db.SaveChanges();

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Listening).ToListAsync();
        var content = ResourceBankItemContent.Deserialize<ListeningPassageContent>(rows[0].ContentJson);
        content.AudioDurationSeconds.Should().BeNull();
    }

    [Fact]
    public async Task Publishing_speaking_prompt_candidate_creates_exactly_one_row_with_mapped_fields()
    {
        var source = SeedSource();
        var rawJson = """{"title":"Deadline negotiation","scenario":"Role-play: negotiate a deadline extension with your manager.","durationSeconds":"60"}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.SpeakingPrompt, "Deadline negotiation", rawJson);
        MakePublishReady(candidate, primarySkill: "speaking");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        result.PublishedEntityType.Should().Be("CefrSpeakingPrompt");

        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Speaking).ToListAsync();
        rows.Should().HaveCount(1);
        var content = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(rows[0].ContentJson);
        content.Title.Should().Be("Deadline negotiation");
        content.PromptText.Should().Be("Role-play: negotiate a deadline extension with your manager.");
        content.SuggestedDurationSeconds.Should().Be(60);
        rows[0].CefrLevel.Should().Be("A1");
        rows[0].SourceId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Speaking_prompt_without_a_title_derives_one_from_the_scenario_text()
    {
        var source = SeedSource();
        var rawJson = """{"scenario":"Order food at a restaurant and ask about allergens."}""";
        var (_, raw) = SeedRunAndRaw(source, rawJson);
        var candidate = SeedCandidate(raw, ResourceCandidateType.SpeakingPrompt, "Order food at a restaurant and ask about allergens.", rawJson);
        MakePublishReady(candidate, primarySkill: "speaking");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeTrue();
        var rows = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Speaking).ToListAsync();
        var content = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(rows[0].ContentJson);
        content.Title.Should().Be("Order food at a restaurant and ask about allergens.");
    }

    [Fact]
    public async Task ActivityTemplateCandidate_publish_is_deferred_and_blocked()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"formio":"{\"components\":[]}"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.ActivityTemplateCandidate, "activity",
            """{"formio":"{\"components\":[]}"}""");
        MakePublishReady(candidate, primarySkill: "speaking");

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("deferred in Phase E4"));
    }

    [Fact]
    public async Task Unknown_typed_candidate_publish_returns_error_and_creates_zero_rows()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"mystery":"field"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.Unknown, "mystery", """{"mystery":"field"}""");
        MakePublishReady(candidate, primarySkill: null);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("no supported bank publish target"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Grammar)).Should().Be(0);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingReference)).Should().Be(0);
    }

    [Fact]
    public async Task Candidate_with_unparseable_typed_content_cannot_be_published()
    {
        // Phase 4.5 — a candidate whose NormalizedJson isn't parseable JSON at all (bypassing the
        // typed editor's own validation, e.g. via direct data corruption) fails the publish-time
        // typed-schema gate rather than being silently mapped with blank fields.
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", "not valid json {{{");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        (await _db.ResourceBankItems.CountAsync()).Should().Be(0);
    }

    // ── Idempotency + raw-record integrity ──────────────────────────────────────

    [Fact]
    public async Task Duplicate_publish_call_is_idempotent()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);

        var first = await _sut.PublishAsync(candidate.Id, null);
        var second = await _sut.PublishAsync(candidate.Id, null);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.PublishedEntityId.Should().Be(first.PublishedEntityId);

        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(1);
    }

    [Fact]
    public async Task Publishing_never_mutates_the_raw_record()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(
            await _db.ResourceRawRecords.AsNoTracking().FirstAsync(r => r.Id == raw.Id));

        var result = await _sut.PublishAsync(candidate.Id, null);
        result.Success.Should().BeTrue();

        var afterJson = System.Text.Json.JsonSerializer.Serialize(
            await _db.ResourceRawRecords.AsNoTracking().FirstAsync(r => r.Id == raw.Id));

        afterJson.Should().Be(beforeJson);
    }

    [Fact]
    public async Task Publish_of_nonexistent_candidate_throws_validation_exception()
    {
        Func<Task> act = async () => await _sut.PublishAsync(Guid.NewGuid(), null);
        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    // ── Phase 4.2 — Import Execution Plan provenance gate ───────────────────────

    [Fact]
    public async Task Candidate_whose_run_has_no_import_package_cannot_publish()
    {
        var source = SeedSource();
        // Deliberately bypasses SeedRunAndRaw's default provenance — simulates a legacy/orphan run.
        var run = new ResourceImportRun(source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();
        var raw = new ResourceRawRecord(run.Id, $"rawhash-{Guid.NewGuid():N}", "en", "row", rawJson: """{"word":"hello"}""");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("no associated Import Package"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }

    [Fact]
    public async Task Candidate_whose_package_has_no_approved_plan_cannot_publish()
    {
        var source = SeedSource();
        var package = new ImportPackage(source.Id, "test-package", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        _db.SaveChanges();
        // No ImportProfile ever approved for this package.
        var run = new ResourceImportRun(
            source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow,
            importPackageId: package.Id);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();
        var raw = new ResourceRawRecord(run.Id, $"rawhash-{Guid.NewGuid():N}", "en", "row", rawJson: """{"word":"hello"}""");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        MakePublishReady(candidate);

        var result = await _sut.PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("no approved Import Execution Plan"));
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(0);
    }
}
