namespace Estudarias.Domain.Common.Exceptions;

/// <summary>
/// O recurso pedido não existe (um tópico ou uma lição com aquele id). Também serve para o que existe mas não é do aluno, quando
/// dizer que existe revelaria o dado de outro (o cartão de revisão de outro aluno).
/// </summary>
public sealed class DomainNotFoundException(string code, string message)
    : BaseDomainException(code, message);
