namespace Estudarias.Domain.Answers;

/// <summary>
/// O resultado da correção: se a alternativa escolhida está certa, qual é a correta e, se errou, o tipo de erro que a
/// alternativa revela.
/// </summary>
public sealed record AnswerDiagnosis(bool IsCorrect, Guid CorrectAlternativeId, Guid? ErrorTypeId);
