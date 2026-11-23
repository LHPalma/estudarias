using Estudarias.Api.Links;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Students;

namespace Estudarias.Api.Contracts.Trail;

/// <summary>
/// Um tópico da trilha. <c>Status</c> é <c>locked</c>, <c>available</c> ou <c>mastered</c> (texto em minúsculas, como o contrato);
/// <c>Score</c> é nulo enquanto o aluno não respondeu nada no tópico.
/// </summary>
public sealed record TrailTopicResponse(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    decimal? Score,
    IReadOnlyList<Guid> Prerequisites,
    IReadOnlyList<Link> Links)
{
    public static TrailTopicResponse From(TrailTopicView topic, IReadOnlyList<Link> links) =>
        new(topic.Id, topic.Slug, topic.Name, StatusText(topic.Status), topic.Score, topic.Prerequisites, links);

    // Explícito, e não o ToString() do enum: o texto é contrato com o app, e renomear um membro não pode mudá-lo sem querer.
    private static string StatusText(TopicStatus status) => status switch
    {
        TopicStatus.Locked => "locked",
        TopicStatus.Available => "available",
        TopicStatus.Mastered => "mastered",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Topic status is not valid."),
    };
}

/// <summary>
/// A trilha do aluno logado (<c>GET /trail</c>). Os <c>links</c> são montados pelo controller, que conhece as rotas (ADR-004).
/// </summary>
public sealed record TrailResponse(
    decimal MasteryThreshold,
    int MasteredTopics,
    int TotalTopics,
    int DueReviews,
    IReadOnlyList<TrailTopicResponse> Topics,
    IReadOnlyList<Link> Links)
{
    public static TrailResponse From(TrailView trail, IReadOnlyList<Link> links) =>
        new(
            trail.MasteryThreshold,
            trail.MasteredTopics,
            trail.TotalTopics,
            trail.DueReviews,
            [.. trail.Topics.Select(topic => TrailTopicResponse.From(topic, []))],
            links);
}
