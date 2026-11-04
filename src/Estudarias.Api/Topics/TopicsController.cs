using Estudarias.Api.Links;
using Estudarias.Application.Topics.GetTopic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Topics;

[ApiController]
[Authorize]
[Route("topics")]
public sealed class TopicsController : ControllerBase
{
    public const string GetTopicRoute = "GetTopic";
    public const string StartLessonRoute = "StartLesson";

    [HttpGet("{id:guid}", Name = GetTopicRoute)]
    public async Task<ActionResult<TopicResponse>> Get(
        Guid id, [FromServices] GetTopicHandler getTopic, CancellationToken cancellationToken)
    {
        var topic = await getTopic.HandleAsync(id, cancellationToken);
        if (topic is null)
        {
            return NotFound();
        }

        return new TopicResponse(topic.Id, topic.Name, topic.Slug, topic.Status, BuildLinks(topic));
    }

    // Só existe para a rota nomeada que o link start-lesson aponta; a task das lições implementa.
    [HttpPost("{id:guid}/lessons", Name = StartLessonRoute)]
    public IActionResult StartLesson(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    private List<Link> BuildLinks(TopicDetails topic)
    {
        var values = new { id = topic.Id };
        List<Link> links = [Url.LinkTo(LinkRelations.Self, HttpMethods.Get, GetTopicRoute, values)];

        if (topic.IsUnlocked)
        {
            links.Add(Url.LinkTo(LinkRelations.StartLesson, HttpMethods.Post, StartLessonRoute, values));
        }

        return links;
    }
}
