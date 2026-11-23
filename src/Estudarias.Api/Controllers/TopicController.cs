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
public sealed class TopicController(
    ICurrentUser currentUser, ITopicReads topics, ITopicAttemptsReads topicAttempts) : ControllerBase
{
    /// <summary>O nome da rota do histórico de respostas: é por ele que o link <c>attempts</c> do tópico chega aqui.</summary>
    public const string GetTopicAttemptsRoute = "GetTopicAttempts";

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

        var links = new List<Link>
        {
            Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetTopicRoute, new { topicId }),
            Url.LinkTo(LinkRelations.Trail, HttpMethods.Get, TrailController.GetTrailRoute),
        };

        // O histórico de respostas só existe para quem pode praticar o tópico: o bloqueado responde 403 (RN-TRL-09).
        if (topic.Status != TopicStatus.Locked)
        {
            links.Add(Url.LinkTo(LinkRelations.Attempts, HttpMethods.Get, GetTopicAttemptsRoute, new { topicId }));
        }

        return Ok(TopicResponse.From(topic, LessonLinks, links));
    }

    /// <summary>
    /// As respostas do aluno no tópico que entram na nota (as 10 mais recentes), da mais recente para a mais antiga: o histórico que
    /// explica por que a nota subiu ou caiu. Um tópico liberado sem respostas devolve a lista vazia.
    /// </summary>
    /// <remarks>Um tópico bloqueado responde 403 (<c>student.topic.locked</c>): o link <c>attempts</c> do tópico só existe quando ele está liberado.</remarks>
    [HttpGet("{topicId:guid}/attempts", Name = GetTopicAttemptsRoute)]
    [ProducesResponseType<TopicAttemptsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetAttempts(Guid topicId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        var attempts = await topicAttempts.FindAsync(userId, topicId, cancellationToken);

        return Ok(TopicAttemptsResponse.From(
            attempts,
            [
                Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetTopicAttemptsRoute, new { topicId }),
                Url.LinkTo(LinkRelations.Topic, HttpMethods.Get, GetTopicRoute, new { topicId }),
            ]));
    }
}
