using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Exceptions;

namespace LinguaCoach.UnitTests.Domain;

public sealed class StudentProfileTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Language Persian() => new("fa", "Persian", LanguageDirection.Rtl);
    private static Language English() => new("en", "English", LanguageDirection.Ltr);

    private static LanguagePair FaEnPair()
    {
        var fa = Persian();
        var en = English();
        return new LanguagePair(fa, en);
    }

    private static CareerProfile DocumentController(LanguagePair pair) =>
        new("Document Controller", "English for document control workflows.", pair);

    // ── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void NewStudent_HasNotStartedOnboarding()
    {
        var student = new StudentProfile(Guid.NewGuid());

        student.OnboardingStatus.Should().Be(OnboardingStatus.NotStarted);
        student.LastCompletedStep.Should().Be(OnboardingStep.None);
        student.LanguagePairId.Should().BeNull();
        student.CareerProfileId.Should().BeNull();
        student.SkillFocus.Should().BeNull();
    }

    [Fact]
    public void NewStudent_WithEmptyUserId_Throws()
    {
        var act = () => new StudentProfile(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    // ── Happy path: all four steps in order ─────────────────────────────────

    [Fact]
    public void SetLanguagePair_AdvancesToInProgressLanguageStep()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();

        student.SetLanguagePair(pair);

        student.OnboardingStatus.Should().Be(OnboardingStatus.InProgress);
        student.LastCompletedStep.Should().Be(OnboardingStep.Language);
        student.LanguagePairId.Should().Be(pair.Id);
        student.LanguagePair.Should().BeSameAs(pair);
    }

    [Fact]
    public void SetSessionPreference_AfterLanguage_AdvancesToPreferenceStep()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);

        student.SetSessionPreference(15);

        student.LastCompletedStep.Should().Be(OnboardingStep.Preference);
        student.PreferredSessionDurationMinutes.Should().Be(15);
    }

    [Fact]
    public void SetCareerProfile_AfterPreference_AdvancesToCareerStep()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);
        student.SetSessionPreference(20);

        var profile = DocumentController(pair);
        student.SetCareerProfile(profile);

        student.LastCompletedStep.Should().Be(OnboardingStep.Career);
        student.CareerProfileId.Should().Be(profile.Id);
        student.CareerProfile.Should().BeSameAs(profile);
    }

    [Fact]
    public void SetSkillFocus_AfterCareer_CompletesOnboarding()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);
        student.SetSessionPreference(15);
        student.SetCareerProfile(DocumentController(pair));

        student.SetSkillFocus(SkillFocus.Writing);

        student.OnboardingStatus.Should().Be(OnboardingStatus.Complete);
        student.LastCompletedStep.Should().Be(OnboardingStep.Skill);
        student.SkillFocus.Should().Be(SkillFocus.Writing);
    }

    // ── Null argument guards ─────────────────────────────────────────────────

    [Fact]
    public void SetLanguagePair_WithNull_Throws()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var act = () => student.SetLanguagePair(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetSessionPreference_WithZero_Throws()
    {
        var student = new StudentProfile(Guid.NewGuid());
        student.SetLanguagePair(FaEnPair());
        var act = () => student.SetSessionPreference(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCareerProfile_WithNull_Throws()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);
        student.SetSessionPreference(15);
        var act = () => student.SetCareerProfile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public void CompletedOnboarding_RejectsFurtherModification()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);
        student.SetSessionPreference(15);
        student.SetCareerProfile(DocumentController(pair));
        student.SetSkillFocus(SkillFocus.Writing);

        // Profile is immutable once complete — any step throws.
        var act = () => student.SetLanguagePair(FaEnPair());
        act.Should().Throw<DomainException>();
        student.OnboardingStatus.Should().Be(OnboardingStatus.Complete);
    }

    // ── Out-of-order step enforcement ────────────────────────────────────────

    [Fact]
    public void SetPreference_BeforeLanguage_ThrowsOutOfOrder()
    {
        var student = new StudentProfile(Guid.NewGuid());

        var ex = Assert.Throws<OnboardingStepOutOfOrderException>(
            () => student.SetSessionPreference(15));
        ex.RequestedStep.Should().Be(OnboardingStep.Preference);
        ex.ExpectedStep.Should().Be(OnboardingStep.Language);
    }

    [Fact]
    public void SetCareer_BeforePreference_ThrowsOutOfOrder()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);

        var ex = Assert.Throws<OnboardingStepOutOfOrderException>(
            () => student.SetCareerProfile(DocumentController(pair)));
        ex.RequestedStep.Should().Be(OnboardingStep.Career);
        ex.ExpectedStep.Should().Be(OnboardingStep.Preference);
    }

    [Fact]
    public void SetSkill_BeforeCareer_ThrowsOutOfOrder()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);
        student.SetSessionPreference(15);

        var ex = Assert.Throws<OnboardingStepOutOfOrderException>(
            () => student.SetSkillFocus(SkillFocus.Vocabulary));
        ex.RequestedStep.Should().Be(OnboardingStep.Skill);
        ex.ExpectedStep.Should().Be(OnboardingStep.Career);
    }

    [Fact]
    public void SetSkillFocus_FromNotStarted_ThrowsOutOfOrder()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var ex = Assert.Throws<OnboardingStepOutOfOrderException>(
            () => student.SetSkillFocus(SkillFocus.Writing));
        ex.RequestedStep.Should().Be(OnboardingStep.Skill);
        ex.ExpectedStep.Should().Be(OnboardingStep.Language);
    }

    [Fact]
    public void SetCareer_FromNotStarted_ThrowsOutOfOrder()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        var ex = Assert.Throws<OnboardingStepOutOfOrderException>(
            () => student.SetCareerProfile(DocumentController(pair)));
        ex.RequestedStep.Should().Be(OnboardingStep.Career);
        ex.ExpectedStep.Should().Be(OnboardingStep.Language);
    }

    // ── Cross-language-pair guard ────────────────────────────────────────────

    [Fact]
    public void SetCareerProfile_FromDifferentLanguagePair_ThrowsDomainException()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var pair = FaEnPair();
        student.SetLanguagePair(pair);
        student.SetSessionPreference(15);

        var otherPair = new LanguagePair(new Language("de", "German", LanguageDirection.Ltr), English());
        var germanProfile = new CareerProfile("Engineer", "German engineering context.", otherPair);

        var act = () => student.SetCareerProfile(germanProfile);
        act.Should().Throw<DomainException>();
    }
}
