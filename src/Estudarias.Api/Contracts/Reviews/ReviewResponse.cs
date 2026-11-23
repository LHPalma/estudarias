using Estudarias.Api.Links;
using Estudarias.Application.Reviews;

namespace Estudarias.Api.Contracts.Reviews;

/// <summary>A revisão feita: o cartão e a próxima revisão, calculada a partir do instante em que o aluno revisou.</summary>
public sealed record ReviewResponse(Guid Id, DateTimeOffset NextReviewAt, IReadOnlyList<Link> Links)
{
    public static ReviewResponse From(ReviewFlashcardResult result, IReadOnlyList<Link> links) =>
        new(result.Id, result.NextReviewAt, links);
}
