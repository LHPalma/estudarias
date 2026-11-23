using Estudarias.Api.Links;
using Estudarias.Application.Reviews.Queries;

namespace Estudarias.Api.Contracts.Reviews;

/// <summary>O tópico de um cartão: só o id e o nome.</summary>
public sealed record DueCardTopicResponse(Guid Id, string Name);

/// <summary>
/// Um cartão vencido, com a pergunta e a resposta: virar o cartão é coisa do app, e a revisão funciona sem uma segunda chamada.
/// <c>Id</c> é o do cartão do aluno, o que <c>POST /reviews/{id}</c> usa. O link <c>review</c> só aparece nos vencidos, e a lista só
/// traz vencidos.
/// </summary>
public sealed record DueCardResponse(
    Guid Id,
    string Question,
    string Answer,
    DueCardTopicResponse Topic,
    DateTimeOffset NextReviewAt,
    IReadOnlyList<Link> Links);

/// <summary>As revisões do dia do aluno logado (<c>GET /reviews/due</c>), do cartão mais atrasado ao mais recente.</summary>
public sealed record DueReviewsResponse(int Count, IReadOnlyList<DueCardResponse> Items, IReadOnlyList<Link> Links)
{
    /// <param name="cardLinks">Os links de cada cartão, pelo id dele: o controller os monta, que conhece as rotas.</param>
    public static DueReviewsResponse From(
        DueReviewsView due, Func<DueCardView, IReadOnlyList<Link>> cardLinks, IReadOnlyList<Link> links) =>
        new(
            due.Count,
            [.. due.Items.Select(card => new DueCardResponse(
                card.Id,
                card.Question,
                card.Answer,
                new DueCardTopicResponse(card.TopicId, card.TopicName),
                card.NextReviewAt,
                cardLinks(card)))],
            links);
}
