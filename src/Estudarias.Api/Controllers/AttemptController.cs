using Estudarias.Api.Contracts.Attempts;
using Estudarias.Api.Links;
using Estudarias.Application.Attempts;
using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// O aluno responde um exercício. Protegido pela política padrão da API: sem token, ou com token vencido, o middleware do JWT responde
/// 401 antes de chegar aqui. É uma escrita, então passa por um caso de uso (ADR-002). O aluno é sempre o dono do token, e a rota só
/// recebe o id do exercício.
/// </summary>
[ApiController]
[Route("exercises")]
[Produces("application/json")]
public sealed class AttemptController(ICurrentUser currentUser, SubmitAttemptHandler submitAttempt) : ControllerBase
{
    /// <summary>O nome da rota de responder: é por ele que o link <c>answer</c> de cada exercício da lição chega aqui.</summary>
    public const string SubmitAttemptRoute = "SubmitAttempt";

    /// <summary>
    /// Registra a resposta e devolve, de uma vez, o que a tela do retorno precisa: o retorno conforme o grupo experimental, a nota nova
    /// do tópico, os tópicos liberados e o cartão de revisão criado pelo erro.
    /// </summary>
    /// <remarks>
    /// Responde <c>201</c> sem o cabeçalho <c>Location</c>: a tentativa não tem um recurso próprio para ler (o histórico do tópico lista
    /// as respostas, mas não há <c>GET</c> de uma tentativa), e o <c>Url.LinkTo</c> recusa uma rota que não existe (ADR-004).
    /// </remarks>
    [HttpPost("{exerciseId:guid}/attempts", Name = SubmitAttemptRoute)]
    [ProducesResponseType<AttemptResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Post(Guid exerciseId, AttemptRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        var result = await submitAttempt.Handle(
            userId,
            new SubmitAttemptCommand(exerciseId, request.AlternativeId ?? Guid.Empty, request.ResponseSeconds),
            cancellationToken);

        var links = new List<Link>
        {
            Url.LinkTo(LinkRelations.Topic, HttpMethods.Get, TopicController.GetTopicRoute, new { topicId = result.Topic.Id }),
            Url.LinkTo(LinkRelations.Reviews, HttpMethods.Get, ReviewController.GetDueReviewsRoute),
        };

        return StatusCode(StatusCodes.Status201Created, AttemptResponse.From(result, links));
    }
}
