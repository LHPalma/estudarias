using Estudarias.Api.Contracts.Reviews;
using Estudarias.Api.Links;
using Estudarias.Application.Identity;
using Estudarias.Application.Reviews;
using Estudarias.Application.Reviews.Queries;
using Estudarias.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// A revisão espaçada de quem está logado. Protegida pela política padrão da API: sem token, ou com token vencido, o middleware do JWT
/// responde 401 antes de chegar aqui. O aluno é sempre o dono do token, e nenhuma rota recebe id de aluno. A lista é uma leitura, e a
/// revisão é uma escrita, que passa por um caso de uso (ADR-002).
/// </summary>
[ApiController]
[Route("reviews")]
[Produces("application/json")]
public sealed class ReviewController(
    ICurrentUser currentUser, IReviewReads reviews, ReviewFlashcardHandler reviewFlashcard) : ControllerBase
{
    /// <summary>O nome da rota das revisões do dia: é por ele que os links (os da trilha, da resposta e da revisão) chegam aqui.</summary>
    public const string GetDueReviewsRoute = "GetDueReviews";

    /// <summary>O nome da rota de revisar um cartão: é por ele que o link <c>review</c> de cada cartão chega aqui.</summary>
    public const string ReviewCardRoute = "ReviewCard";

    /// <summary>Os cartões do aluno com a revisão vencida, do mais atrasado ao mais recente, já com a pergunta e a resposta.</summary>
    /// <remarks>Sem cartões vencidos (ou sem nenhum cartão), responde 200 com <c>count: 0</c> e a lista vazia.</remarks>
    [HttpGet("due", Name = GetDueReviewsRoute)]
    [ProducesResponseType<DueReviewsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> GetDue(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        var due = await reviews.DueAsync(userId, cancellationToken);

        // A lista só traz cartões vencidos, e é neles que a revisão é oferecida (RN-REV-09, RN-TRL-09).
        return Ok(DueReviewsResponse.From(
            due,
            card => [Url.LinkTo(LinkRelations.Review, HttpMethods.Post, ReviewCardRoute, new { studentFlashcardId = card.Id })],
            [
                Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetDueReviewsRoute),
                Url.LinkTo(LinkRelations.Trail, HttpMethods.Get, TrailController.GetTrailRoute),
            ]));
    }

    /// <summary>Registra como foi a revisão e agenda a próxima a partir do instante em que o aluno revisou.</summary>
    /// <remarks>
    /// Revisar antes do vencimento é aceito: oferecer a revisão só no vencimento é regra da lista, e não do agregado. Um cartão de outro
    /// aluno responde 404, igual a um que não existe.
    /// </remarks>
    [HttpPost("{studentFlashcardId:guid}", Name = ReviewCardRoute)]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Post(Guid studentFlashcardId, ReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        var result = await reviewFlashcard.Handle(
            userId, new ReviewFlashcardCommand(studentFlashcardId, request.ToRating()), cancellationToken);

        return Ok(ReviewResponse.From(
            result,
            [Url.LinkTo(LinkRelations.Reviews, HttpMethods.Get, GetDueReviewsRoute)]));
    }
}
