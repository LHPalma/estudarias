namespace Estudarias.Domain.Answers;

/// <summary>
/// O que o aluno vê depois de responder, sem texto pronto: as mensagens são do app. O nome e o feedback do tipo de erro
/// só vêm em <see cref="AnswerFeedbackKind.IncorrectWithDiagnosis"/>.
/// </summary>
public sealed record AnswerFeedback(
    AnswerFeedbackKind Kind,
    string CorrectAnswer,
    string? ErrorTypeName,
    string? ErrorTypeFeedback);
