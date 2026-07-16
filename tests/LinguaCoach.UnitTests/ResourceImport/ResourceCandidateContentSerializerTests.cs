using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.5 — the central typed candidate content parse/validate/serialize service. Pure, no
/// database — mirrors the constructor-injection-free style of other stateless service tests in
/// this project (e.g. ResourceLanguageHeuristicTests).
/// </summary>
public sealed class ResourceCandidateContentSerializerTests
{
    private readonly ResourceCandidateContentSerializer _sut = new();

    // ── 1. Each supported candidate type validates a valid, canonical-shaped payload. ──────────

    [Theory]
    [InlineData(ResourceCandidateType.VocabularyEntry, """{"word":"hello","definition":"a greeting","partOfSpeech":"interjection"}""")]
    [InlineData(ResourceCandidateType.GrammarProfileEntry, """{"title":"present simple","explanation":"habitual actions"}""")]
    [InlineData(ResourceCandidateType.ReadingPassage, """{"passageText":"A short passage about daily life."}""")]
    [InlineData(ResourceCandidateType.ListeningPassage, """{"title":"Morning News","transcript":"Good morning."}""")]
    [InlineData(ResourceCandidateType.SpeakingPrompt, """{"title":"Order food","promptText":"Order food at a restaurant."}""")]
    [InlineData(ResourceCandidateType.WritingPrompt, """{"title":"Email reply","promptText":"Write a reply to your manager."}""")]
    public void Each_supported_candidate_type_validates_a_valid_canonical_payload(ResourceCandidateType type, string json)
    {
        var parsed = _sut.Parse(type, json);
        parsed.Success.Should().BeTrue();

        var validation = _sut.Validate(type, parsed.Content!);
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
        parsed.WasLegacyMapped.Should().BeFalse("the payload used the canonical field names directly");
    }

    // ── 2. Missing required fields fail with structured errors. ────────────────────────────────

    [Fact]
    public void Vocabulary_missing_word_fails_validation_with_a_structured_error()
    {
        var parsed = _sut.Parse(ResourceCandidateType.VocabularyEntry, """{"definition":"a greeting"}""");
        var validation = _sut.Validate(ResourceCandidateType.VocabularyEntry, parsed.Content!);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.FieldName == "word");
    }

    [Fact]
    public void Grammar_missing_title_fails_validation_with_a_structured_error()
    {
        var parsed = _sut.Parse(ResourceCandidateType.GrammarProfileEntry, """{"explanation":"habitual actions"}""");
        var validation = _sut.Validate(ResourceCandidateType.GrammarProfileEntry, parsed.Content!);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.FieldName == "title");
    }

    [Fact]
    public void Reading_missing_passage_text_fails_validation_with_a_structured_error()
    {
        var parsed = _sut.Parse(ResourceCandidateType.ReadingPassage, """{"title":"Untitled"}""");
        var validation = _sut.Validate(ResourceCandidateType.ReadingPassage, parsed.Content!);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.FieldName == "passageText");
    }

    [Fact]
    public void Speaking_missing_prompt_text_and_title_fails_with_both_structured_errors()
    {
        var parsed = _sut.Parse(ResourceCandidateType.SpeakingPrompt, """{"suggestedDurationSeconds":60}""");
        var validation = _sut.Validate(ResourceCandidateType.SpeakingPrompt, parsed.Content!);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.FieldName == "title");
        validation.Errors.Should().Contain(e => e.FieldName == "promptText");
    }

    [Fact]
    public void Writing_missing_prompt_text_fails_with_a_structured_error()
    {
        var parsed = _sut.Parse(ResourceCandidateType.WritingPrompt, """{"title":"Untitled"}""");
        var validation = _sut.Validate(ResourceCandidateType.WritingPrompt, parsed.Content!);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.FieldName == "promptText");
    }

    // ── 3. Unknown fields/types fail safely — never throws, never silently accepts. ────────────

    [Fact]
    public void ActivityTemplateCandidate_has_no_typed_schema_and_parse_fails_safely()
    {
        _sut.SupportsTypedSchema(ResourceCandidateType.ActivityTemplateCandidate).Should().BeFalse();

        var parsed = _sut.Parse(ResourceCandidateType.ActivityTemplateCandidate, """{"formIo":"{}"}""");

        parsed.Success.Should().BeFalse();
        parsed.Content.Should().BeNull();
        parsed.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Unknown_candidate_type_has_no_typed_schema_and_parse_fails_safely()
    {
        _sut.SupportsTypedSchema(ResourceCandidateType.Unknown).Should().BeFalse();

        var parsed = _sut.Parse(ResourceCandidateType.Unknown, """{"anything":"goes"}""");

        parsed.Success.Should().BeFalse();
    }

    [Fact]
    public void Empty_json_object_fails_to_parse_safely_rather_than_producing_a_blank_typed_content()
    {
        var parsed = _sut.Parse(ResourceCandidateType.VocabularyEntry, "{}");

        parsed.Success.Should().BeFalse();
        parsed.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Malformed_json_fails_to_parse_safely_rather_than_throwing()
    {
        var parsed = _sut.Parse(ResourceCandidateType.VocabularyEntry, "not json at all {{{");

        parsed.Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_a_content_type_that_does_not_match_the_declared_candidate_type()
    {
        var vocabularyContent = new VocabularyCandidateContent("hello", "a greeting");

        var validation = _sut.Validate(ResourceCandidateType.GrammarProfileEntry, vocabularyContent);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.FieldName == "candidateType");
    }

    // ── Legacy alias compatibility — a pre-4.5 row using an alias column name still parses. ────

    [Theory]
    [InlineData("""{"lemma":"hello"}""")]
    [InlineData("""{"headword":"hello"}""")]
    public void Vocabulary_legacy_alias_columns_still_parse_and_are_flagged_as_legacy_mapped(string json)
    {
        var parsed = _sut.Parse(ResourceCandidateType.VocabularyEntry, json);

        parsed.Success.Should().BeTrue();
        ((VocabularyCandidateContent)parsed.Content!).Word.Should().Be("hello");
        parsed.WasLegacyMapped.Should().BeTrue();
    }

    [Fact]
    public void Grammar_legacy_grammarKey_alias_still_parses()
    {
        var parsed = _sut.Parse(
            ResourceCandidateType.GrammarProfileEntry, """{"grammarKey":"present simple","explanation":"habitual actions"}""");

        parsed.Success.Should().BeTrue();
        ((GrammarCandidateContent)parsed.Content!).Title.Should().Be("present simple");
    }

    [Fact]
    public void CanonicalTextFallback_fills_the_primary_field_when_the_row_has_none()
    {
        var parsed = _sut.Parse(ResourceCandidateType.VocabularyEntry, """{"definition":"a greeting"}""", "hello");

        parsed.Success.Should().BeTrue();
        ((VocabularyCandidateContent)parsed.Content!).Word.Should().Be("hello");

        var validation = _sut.Validate(ResourceCandidateType.VocabularyEntry, parsed.Content!);
        validation.IsValid.Should().BeTrue("the CanonicalText fallback filled the required Word field");
    }

    [Fact]
    public void Serialize_round_trips_through_Parse_without_losing_fields()
    {
        var content = new VocabularyCandidateContent("hello", "a greeting", "interjection", new[] { "Hello!", "Well, hello." });

        var json = _sut.Serialize(content);
        var reparsed = _sut.Parse(ResourceCandidateType.VocabularyEntry, json);

        reparsed.Success.Should().BeTrue();
        var reparsedContent = (VocabularyCandidateContent)reparsed.Content!;
        reparsedContent.Word.Should().Be("hello");
        reparsedContent.Definition.Should().Be("a greeting");
        reparsedContent.PartOfSpeech.Should().Be("interjection");
        reparsedContent.Examples.Should().BeEquivalentTo(new[] { "Hello!", "Well, hello." });
    }
}
