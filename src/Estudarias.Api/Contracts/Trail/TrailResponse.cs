using Estudarias.Api.Links;
using Estudarias.Application.Trail.Queries;

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
        new(topic.Id, topic.Slug, topic.Name, TopicStatusText.Of(topic.Status), topic.Score, topic.Prerequisites, links);
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
    /// <param name="topicLinks">Os links de cada item da trilha, pelo tópico dele: o controller os monta, que conhece as rotas.</param>
    public static TrailResponse From(
        TrailView trail, Func<TrailTopicView, IReadOnlyList<Link>> topicLinks, IReadOnlyList<Link> links) =>
        new(
            trail.MasteryThreshold,
            trail.MasteredTopics,
            trail.TotalTopics,
            trail.DueReviews,
            [.. trail.Topics.Select(topic => TrailTopicResponse.From(topic, topicLinks(topic)))],
            links);
}
