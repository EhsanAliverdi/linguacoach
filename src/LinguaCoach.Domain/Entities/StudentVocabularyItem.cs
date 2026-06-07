using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A useful word, phrase, or pattern extracted from a student's writing attempt.
/// Lightweight model for review and status tracking.
/// Separate from VocabularyEntry which is designed for full SM-2 spaced repetition.
/// </summary>
public sealed class StudentVocabularyItem : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid? SourceActivityAttemptId { get; private set; }
    public Guid? SourceLearningActivityId { get; private set; }

    public string Term { get; private set; }
    public string? SuggestedPhrase { get; private set; }
    public string MeaningOrExplanation { get; private set; }
    public string? ExampleSentence { get; private set; }

    /// <summary>One of the VocabularyItemCategory constants.</summary>
    public string Category { get; private set; }

    public VocabularyItemStatus Status { get; private set; }
    public VocabularyItemSource Source { get; private set; }

    public int SeenCount { get; private set; }
    public int StrengthScore { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }
    public DateTime? NextReviewAtUtc { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private StudentVocabularyItem()
    {
        Term = string.Empty;
        MeaningOrExplanation = string.Empty;
        Category = string.Empty;
    }

    public StudentVocabularyItem(
        Guid studentProfileId,
        string term,
        string? suggestedPhrase,
        string meaningOrExplanation,
        string? exampleSentence,
        string category,
        VocabularyItemSource source,
        Guid? sourceActivityAttemptId = null,
        Guid? sourceLearningActivityId = null)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(term))
            throw new ArgumentException("Term is required.", nameof(term));
        if (string.IsNullOrWhiteSpace(meaningOrExplanation))
            throw new ArgumentException("MeaningOrExplanation is required.", nameof(meaningOrExplanation));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required.", nameof(category));

        StudentProfileId = studentProfileId;
        Term = NormaliseTerm(term);
        SuggestedPhrase = suggestedPhrase?.Trim();
        MeaningOrExplanation = meaningOrExplanation.Trim();
        ExampleSentence = exampleSentence?.Trim();
        Category = category.Trim().ToLowerInvariant();
        Status = VocabularyItemStatus.New;
        Source = source;
        SeenCount = 1;
        LastSeenAtUtc = DateTime.UtcNow;
        SourceActivityAttemptId = sourceActivityAttemptId;
        SourceLearningActivityId = sourceLearningActivityId;
        UpdatedAt = DateTime.UtcNow;
    }

    private const int MaxStrengthScore = 100;
    private const int MinStrengthScore = 0;
    private const int MasteredThreshold = 90;

    public void RecordSeen()
    {
        SeenCount++;
        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a vocabulary practice attempt. Updates SeenCount, StrengthScore, and Status.
    /// New → Practising on first practice. Practising → Mastered when StrengthScore ≥ 90.
    /// </summary>
    public void RecordPractice(bool correct)
    {
        SeenCount++;
        LastSeenAtUtc = DateTime.UtcNow;
        StrengthScore = Math.Clamp(StrengthScore + (correct ? 10 : -5), MinStrengthScore, MaxStrengthScore);

        if (Status == VocabularyItemStatus.New)
            Status = VocabularyItemStatus.Practising;

        if (Status == VocabularyItemStatus.Practising && StrengthScore >= MasteredThreshold)
            Status = VocabularyItemStatus.Mastered;

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(VocabularyItemStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public static string NormaliseTerm(string term) =>
        term.Trim().ToLowerInvariant();
}
