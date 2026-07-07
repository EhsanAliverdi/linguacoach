using LinguaCoach.Application.FormIo;

namespace LinguaCoach.Infrastructure.FormIo;

/// <summary>DI-registered entry point for <see cref="FormIoQuizAnnotationCodec.Split"/> — the sole
/// authority splitting an admin-authored, quiz-annotated Form.io schema into a student-safe schema
/// and a backend-only ScoringRulesDocument. The actual (I/O-free) logic lives in the Application
/// layer so it's also directly callable from Persistence's seeders, which cannot reference
/// Infrastructure.</summary>
public sealed class FormIoQuizSchemaSplitter : IFormIoQuizSchemaSplitter
{
    public FormIoQuizSplitResult Split(string authoringSchemaJson) => FormIoQuizAnnotationCodec.Split(authoringSchemaJson);
}
