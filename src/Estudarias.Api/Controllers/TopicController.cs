using Estudarias.Api.Contracts.Topics;
using Estudarias.Api.Links;
using Estudarias.Application.Identity;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// O tópico de quem está logado. Protegido pela política padrão da API, e uma leitura: o controller chama a porta de leitura direto
/// (ADR-002). O aluno é sempre o dono do token; a rota só recebe o id do tópico.
/// </summary>
[ApiController]
[Route("topics")]
[Produces("application/json")]
public sealed class TopicController(ICurrentUser currentUser, ITopicReads topics) : ControllerBase
{
    /// <summary>O nome da rota do tópico: é por ele que os links (os da trilha e os da lição) chegam aqui.</summary>
    public const string GetTopicRoute = "GetTopic";

    /// <summary>O tópico: o status, a nota, as tentativas, o que falta para dominar, os pré-requisitos, o que ele libera e as lições.</summary>
    /// <remarks>
    /// Um tópico bloqueado também abre (200), para a tela mostrar o que falta, mas as lições dele vêm sem o link <c>lesson</c>.
    /// </remarks>
    [HttpGet("{topicId:guid}", Name = GetTopicRoute)]
    [ProducesResponseType<TopicResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid topicId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        var topic = await topics.FindAsync(userId, topicId, cancellationToken);

        // A ação de abrir a lição só existe quando o tópico não está bloqueado (RN-TRL-08, RN-TRL-09).
        IReadOnlyList<Link> LessonLinks(TopicLessonView lesson) => topic.Status == TopicStatus.Locked
            ? []
            : [Url.LinkTo(LinkRelations.Lesson, HttpMethods.Get, LessonController.GetLessonRoute, new { lessonId = lesson.Id })];

        return Ok(TopicResponse.From(
            topic,
            LessonLinks,
            [
                Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetTopicRoute, new { topicId }),
                Url.LinkTo(LinkRelations.Trail, HttpMethods.Get, TrailController.GetTrailRoute),
            ]));
    }
}
