namespace LinguaCoach.Domain.Enums;

public enum WritingDryRunSignalOutcome
{
    CandidatePositiveSignal = 0,
    CandidateReviewSignal = 1,
    CandidateNoSignal = 2,
    BlockedMissingScore = 3,
    BlockedFailedEvaluation = 4,
    BlockedUnsupportedProvider = 5,
    BlockedLowConfidence = 6,
    BlockedInsufficientData = 7,
}
