namespace LinguaCoach.Application.FormIo;

/// <summary>Result of splitting an admin-authored Form.io schema (with inline per-component
/// "quiz" annotations) into what a student may see and what must stay backend-only.</summary>
public sealed record FormIoQuizSplitResult(
    /// <summary>The authoring schema with every "quiz" property stripped — safe to serve to
    /// students as FormIoSchemaJson.</summary>
    string StudentSchemaJson,
    /// <summary>ScoringRulesDocument-shaped JSON extracted from each component's "quiz"
    /// annotation, keyed by Form.io component key — backend-only, never served to students.</summary>
    string ScoringRulesJson
);

/// <summary>Splits an admin-authored Form.io schema (edited via the builder's per-component
/// "Quiz" tab) into a student-safe schema and a backend-only scoring-rules document. This is the
/// sole authority for that split — the Angular client never constructs either output itself, so
/// the "students never see the correct answer" invariant does not depend on trusting the browser.
/// Shared by both the onboarding template designer and the placement item-bank designer.</summary>
public interface IFormIoQuizSchemaSplitter
{
    FormIoQuizSplitResult Split(string authoringSchemaJson);
}
