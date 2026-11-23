namespace Estudarias.Api.Contracts.Attempts;

/// <summary>
/// A resposta do aluno a um exercício. <c>AlternativeId</c> ausente ou nulo recebe o mesmo erro de qualquer alternativa que não é do
/// exercício (<c>answer.alternative.notInExercise</c>); <c>ResponseSeconds</c> é opcional, inteiro e de 0 para cima. O aluno e o grupo
/// experimental nunca vêm da requisição: são os do token (RN-RSP-13).
/// </summary>
public sealed record AttemptRequest(Guid? AlternativeId, int? ResponseSeconds);
