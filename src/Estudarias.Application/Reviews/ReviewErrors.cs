using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Application.Reviews;

/// <summary>Os erros da revisão. O código é contrato com o app (<c>docs/api.md</c>), que decide pelo <c>code</c>, e não pela mensagem.</summary>
public static class ReviewErrors
{
    public const string CardNotFound = "review.card.notFound";

    /// <summary>
    /// O cartão não existe, ou é de outro aluno: a resposta é a mesma, para quem pede não ficar sabendo que o cartão de outro aluno
    /// existe (RN-REV-11).
    /// </summary>
    public static DomainNotFoundException NoSuchCard() => new(CardNotFound, "The review card does not exist.");
}
