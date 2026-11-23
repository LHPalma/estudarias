using Estudarias.Api.Contracts.Trail;
using Estudarias.Api.Links;
using Estudarias.Application.Identity;
using Estudarias.Application.Trail;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// A trilha de quem está logado. Protegida pela política padrão da API: sem token, ou com token vencido, o middleware do JWT
/// responde 401 antes de chegar aqui. O aluno é sempre o dono do token; a rota não recebe id de aluno.
/// </summary>
[ApiController]
[Route("trail")]
[Produces("application/json")]
public sealed class TrailController(
    ICurrentUser currentUser,
    UnlockInitialTopicsHandler unlockInitialTopics,
    ITrailReads trails) : ControllerBase
{
    /// <summary>O nome da rota da trilha: é por ele que os links (o do perfil, por exemplo) chegam aqui.</summary>
    public const string GetTrailRoute = "GetTrail";

    /// <summary>A trilha do aluno: os tópicos em ordem de pré-requisito, com o status e a nota de cada um.</summary>
    /// <remarks>
    /// Na primeira chamada de um aluno novo (ou de quem se cadastrou antes de existir conteúdo), os tópicos sem pré-requisito são
    /// liberados antes da leitura: é a única escrita deste endpoint, e é idempotente. Um token válido de uma conta que não existe
    /// mais, ou foi desabilitada, responde 401 (<c>identity.session.invalid</c>), como no <c>/me</c>.
    /// </remarks>
    [HttpGet(Name = GetTrailRoute)]
    [ProducesResponseType<TrailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        await unlockInitialTopics.Handle(userId, cancellationToken);
        var trail = await trails.FindAsync(userId, cancellationToken) ?? throw IdentityErrors.InvalidSession();

        return Ok(TrailResponse.From(
            trail,
            topic => [Url.LinkTo(LinkRelations.Topic, HttpMethods.Get, TopicController.GetTopicRoute, new { topicId = topic.Id })],
            BuildLinks()));
    }

    private List<Link> BuildLinks() =>
    [
        Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetTrailRoute),
        Url.LinkTo(LinkRelations.Reviews, HttpMethods.Get, ReviewController.GetDueReviewsRoute),
    ];
}
