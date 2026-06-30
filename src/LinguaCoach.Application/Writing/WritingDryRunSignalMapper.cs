using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Writing;

/// <summary>
/// Maps a WritingEvaluation to a dry-run signal preview.
/// Pure logic — no DB, no side effects. Never updates mastery, CEFR, or Learning Plan.
/// </summary>
public static class WritingDryRunSignalMapper
{
    public static WritingEvaluationDryRunSignal Map(WritingEvaluation evaluation)
    {
        return MapFromFields(
            evalId: evaluation.Id,
            attemptId: evaluation.ActivityAttemptId,
            studentId: evaluation.StudentProfileId,
            activityId: evaluation.LearningActivityId,
            createdAt: evaluation.CreatedAt,
            providerName: evaluation.ProviderName,
            modelName: evaluation.ModelName,
            status: evaluation.Status,
            overallScore: evaluation.OverallScore,
            grammarScore: evaluation.GrammarScore,
            vocabularyScore: evaluation.VocabularyScore,
            coherenceScore: evaluation.CoherenceScore,
            taskCompletionScore: evaluation.TaskCompletionScore,
            feedbackText: evaluation.FeedbackText,
            correctedText: evaluation.CorrectedText);
    }

    /// <summary>
    /// Overload for use when only projected scalar fields are available (no full entity).
    /// Produces identical results to Map(WritingEvaluation).
    /// </summary>
    public static WritingEvaluationDryRunSignal MapFromFields(
        Guid evalId,
        Guid attemptId,
        Guid studentId,
        Guid activityId,
        DateTime createdAt,
        string? providerName,
        string? modelName,
        WritingEvaluationStatus status,
        double? overallScore,
        double? grammarScore,
        double? vocabularyScore,
        double? coherenceScore,
        double? taskCompletionScore,
        string? feedbackText,
        string? correctedText)
    {
        switch (status)
        {
            case WritingEvaluationStatus.Failed:
                return Blocked(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
                    status, overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
                    WritingDryRunSignalOutcome.BlockedFailedEvaluation,
                    "Evaluation failed.", WritingDryRunConfidenceBand.Low, acceptedForFutureSignal: false);

            case WritingEvaluationStatus.NotSupported:
            case WritingEvaluationStatus.Skipped:
                return Blocked(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
                    status, overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
                    WritingDryRunSignalOutcome.BlockedUnsupportedProvider,
                    "Provider not supported.", WritingDryRunConfidenceBand.Low, acceptedForFutureSignal: false);

            case WritingEvaluationStatus.Pending:
            case WritingEvaluationStatus.Evaluating:
                return Blocked(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
                    status, overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
                    WritingDryRunSignalOutcome.BlockedInsufficientData,
                    "Evaluation not yet complete.", WritingDryRunConfidenceBand.Low, acceptedForFutureSignal: false);

            case WritingEvaluationStatus.Completed:
                break;

            default:
                return Blocked(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
                    status, overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
                    WritingDryRunSignalOutcome.BlockedInsufficientData,
                    "Unknown evaluation status.", WritingDryRunConfidenceBand.Low, acceptedForFutureSignal: false);
        }

        if (overallScore is null)
            return Blocked(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
                status, overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
                WritingDryRunSignalOutcome.BlockedMissingScore,
                "OverallScore not present.", WritingDryRunConfidenceBand.Low, acceptedForFutureSignal: false);

        var confidence = ComputeConfidence(overallScore, grammarScore, vocabularyScore, coherenceScore,
            taskCompletionScore, feedbackText, correctedText);

        if (confidence == WritingDryRunConfidenceBand.Low)
            return Blocked(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
                status, overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
                WritingDryRunSignalOutcome.BlockedLowConfidence,
                "Confidence too low for candidate signal.", confidence, acceptedForFutureSignal: false);

        return ClassifyScore(evalId, attemptId, studentId, activityId, createdAt, providerName, modelName,
            status, overallScore.Value, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
            confidence);
    }

    private static WritingDryRunConfidenceBand ComputeConfidence(
        double? overallScore,
        double? grammarScore,
        double? vocabularyScore,
        double? coherenceScore,
        double? taskCompletionScore,
        string? feedbackText,
        string? correctedText)
    {
        // High: overall present AND at least 2 dimension scores AND feedbackText AND correctedText
        var dimensionCount = new[] { grammarScore, vocabularyScore, coherenceScore, taskCompletionScore }
            .Count(s => s.HasValue);

        if (overallScore.HasValue && dimensionCount >= 2 && feedbackText != null && correctedText != null)
            return WritingDryRunConfidenceBand.High;

        // Medium: overall present AND at least 1 dimension score AND feedbackText
        if (overallScore.HasValue && dimensionCount >= 1 && feedbackText != null)
            return WritingDryRunConfidenceBand.Medium;

        return WritingDryRunConfidenceBand.Low;
    }

    private static WritingEvaluationDryRunSignal ClassifyScore(
        Guid evalId, Guid attemptId, Guid studentId, Guid activityId, DateTime createdAt,
        string? providerName, string? modelName,
        WritingEvaluationStatus status,
        double overallScore,
        double? grammarScore, double? vocabularyScore, double? coherenceScore, double? taskCompletionScore,
        WritingDryRunConfidenceBand confidence)
    {
        // CandidatePositiveSignal: High confidence AND overall >= 75 AND task_completion >= 75
        if (confidence == WritingDryRunConfidenceBand.High
            && overallScore >= 75.0
            && (taskCompletionScore is null || taskCompletionScore >= 75.0))
        {
            var delta = Math.Clamp((overallScore - 60.0) / 100.0, 0.05, 0.25);
            return new WritingEvaluationDryRunSignal
            {
                EvaluationId = evalId,
                AttemptId = attemptId,
                StudentId = studentId,
                ActivityId = activityId,
                CreatedAt = createdAt,
                ProviderName = providerName,
                ModelName = modelName,
                SourceStatus = status,
                CandidateSkill = "Writing",
                OverallScore = overallScore,
                GrammarScore = grammarScore,
                VocabularyScore = vocabularyScore,
                CoherenceScore = coherenceScore,
                TaskCompletionScore = taskCompletionScore,
                ConfidenceBand = confidence,
                Outcome = WritingDryRunSignalOutcome.CandidatePositiveSignal,
                SuggestedMasteryDelta = delta,
                SuggestedReviewNeed = false,
                AcceptedForFutureSignal = true,
                BlockedReason = null,
                Notes = "Dry-run only — not applied to mastery",
            };
        }

        // CandidateReviewSignal: Medium or High AND (overall <= 55 OR coherence <= 55 OR grammar <= 55)
        var weakScore = overallScore <= 55.0
            || (coherenceScore.HasValue && coherenceScore <= 55.0)
            || (grammarScore.HasValue && grammarScore <= 55.0);

        if ((confidence == WritingDryRunConfidenceBand.Medium || confidence == WritingDryRunConfidenceBand.High)
            && weakScore)
        {
            var accepted = confidence >= WritingDryRunConfidenceBand.Medium;
            return new WritingEvaluationDryRunSignal
            {
                EvaluationId = evalId,
                AttemptId = attemptId,
                StudentId = studentId,
                ActivityId = activityId,
                CreatedAt = createdAt,
                ProviderName = providerName,
                ModelName = modelName,
                SourceStatus = status,
                CandidateSkill = "Writing",
                OverallScore = overallScore,
                GrammarScore = grammarScore,
                VocabularyScore = vocabularyScore,
                CoherenceScore = coherenceScore,
                TaskCompletionScore = taskCompletionScore,
                ConfidenceBand = confidence,
                Outcome = WritingDryRunSignalOutcome.CandidateReviewSignal,
                SuggestedMasteryDelta = null,
                SuggestedReviewNeed = true,
                AcceptedForFutureSignal = accepted,
                BlockedReason = null,
                Notes = "Dry-run only — not applied to mastery",
            };
        }

        // CandidateNoSignal
        return new WritingEvaluationDryRunSignal
        {
            EvaluationId = evalId,
            AttemptId = attemptId,
            StudentId = studentId,
            ActivityId = activityId,
            CreatedAt = createdAt,
            ProviderName = providerName,
            ModelName = modelName,
            SourceStatus = status,
            CandidateSkill = "Writing",
            OverallScore = overallScore,
            GrammarScore = grammarScore,
            VocabularyScore = vocabularyScore,
            CoherenceScore = coherenceScore,
            TaskCompletionScore = taskCompletionScore,
            ConfidenceBand = confidence,
            Outcome = WritingDryRunSignalOutcome.CandidateNoSignal,
            SuggestedMasteryDelta = null,
            SuggestedReviewNeed = false,
            AcceptedForFutureSignal = false,
            BlockedReason = null,
            Notes = "Dry-run only — not applied to mastery",
        };
    }

    private static WritingEvaluationDryRunSignal Blocked(
        Guid evalId, Guid attemptId, Guid studentId, Guid activityId, DateTime createdAt,
        string? providerName, string? modelName,
        WritingEvaluationStatus status,
        double? overallScore, double? grammarScore, double? vocabularyScore, double? coherenceScore, double? taskCompletionScore,
        WritingDryRunSignalOutcome outcome,
        string reason,
        WritingDryRunConfidenceBand confidenceBand,
        bool acceptedForFutureSignal) =>
        new()
        {
            EvaluationId = evalId,
            AttemptId = attemptId,
            StudentId = studentId,
            ActivityId = activityId,
            CreatedAt = createdAt,
            ProviderName = providerName,
            ModelName = modelName,
            SourceStatus = status,
            CandidateSkill = "Writing",
            OverallScore = overallScore,
            GrammarScore = grammarScore,
            VocabularyScore = vocabularyScore,
            CoherenceScore = coherenceScore,
            TaskCompletionScore = taskCompletionScore,
            ConfidenceBand = confidenceBand,
            Outcome = outcome,
            SuggestedMasteryDelta = null,
            SuggestedReviewNeed = false,
            AcceptedForFutureSignal = acceptedForFutureSignal,
            BlockedReason = reason,
            Notes = "Dry-run only — not applied to mastery",
        };
}
