using Estudarias.Api.Contracts.Lessons;
using Estudarias.Api.Links;
using Estudarias.Application.Identity;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// A lição de quem está logado. Protegida pela política padrão da API, e uma leitura: o controller chama a porta de leitura direto
/// (ADR-002). A lição nunca revela o gabarito: nenhuma alternativa diz se é a correta.
/// </summary>
[ApiController]
[Route("lessons")]
[Produces("application/json")]
public sealed class LessonController(ICurrentUser currentUser, ILessonReads lessons) : ControllerBase
{
    /// <summary>O nome da rota da lição: é por ele que os links (os do tópico) chegam aqui.</summary>
    public const string GetLessonRoute = "GetLesson";

    /// <summary>O conteúdo da lição e os exercícios dela, com as alternativas na ordem do conteúdo e sem o gabarito.</summary>
    /// <remarks>
    /// A lição de um tópico bloqueado responde 403 (<c>student.topic.locked</c>). O app não deveria chegar aqui, porque o link
    /// <c>lesson</c> não existe para o tópico bloqueado.
    /// </remarks>
    [HttpGet("{lessonId:guid}", Name = GetLessonRoute)]
    [ProducesResponseType<LessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid lessonId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw IdentityErrors.InvalidSession();

        var lesson = await lessons.FindAsync(userId, lessonId, cancellationToken);

        // A lição de um tópico bloqueado nem chega aqui (403), então todo exercício dela pode ser respondido: a ação existe (RN-TRL-09).
        return Ok(LessonResponse.From(
            lesson,
            exercise => [Url.LinkTo(
                LinkRelations.Answer, HttpMethods.Post, AttemptController.SubmitAttemptRoute, new { exerciseId = exercise.Id })],
            [
                Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetLessonRoute, new { lessonId }),
                Url.LinkTo(LinkRelations.Topic, HttpMethods.Get, TopicController.GetTopicRoute, new { topicId = lesson.TopicId }),
            ]));
    }
}
