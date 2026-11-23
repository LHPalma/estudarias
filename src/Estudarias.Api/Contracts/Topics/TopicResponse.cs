using Estudarias.Api.Links;
using Estudarias.Application.Trail.Queries;

namespace Estudarias.Api.Contracts.Topics;

/// <summary>Um pré-requisito do tópico, com o status dele para o aluno: o que falta dominar.</summary>
public sealed record TopicPrerequisiteResponse(Guid Id, string Name, string Status);

/// <summary>Um tópico citado por outro: só o id e o nome.</summary>
public sealed record TopicRefResponse(Guid Id, string Name);

/// <summary>
/// Uma lição do tópico, sem o conteúdo, com o estado dela para o aluno: <c>Status</c> é <c>notStarted</c>, <c>inProgress</c> ou
/// <c>completed</c>, e <c>CompletedExercises</c> de <c>TotalExercises</c> diz quantos exercícios ele já acertou. O link <c>lesson</c> só
/// existe quando o tópico não está bloqueado: o bloqueado abre, mas não se pratica (RN-TRL-08).
/// </summary>
public sealed record TopicLessonResponse(
    Guid Id,
    int Order,
    string Title,
    string Status,
    int CompletedExercises,
    int TotalExercises,
    IReadOnlyList<Link> Links);

/// <summary>
/// O tópico do aluno logado (<c>GET /topics/{id}</c>). <c>Status</c> é <c>locked</c>, <c>available</c> ou <c>mastered</c>;
/// <c>Score</c> é nulo enquanto o aluno não respondeu nada. Dominar exige <c>Score >= MasteryThreshold</c> <b>e</b>
/// <c>AttemptCount >= MinAttempts</c>. Os <c>links</c> são montados pelo controller, que conhece as rotas (ADR-004).
/// </summary>
public sealed record TopicResponse(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    decimal? Score,
    int AttemptCount,
    decimal MasteryThreshold,
    int MinAttempts,
    IReadOnlyList<TopicPrerequisiteResponse> Prerequisites,
    IReadOnlyList<TopicRefResponse> Unlocks,
    IReadOnlyList<TopicLessonResponse> Lessons,
    IReadOnlyList<Link> Links)
{
    /// <param name="lessonLinks">Os links de cada lição, pelo id dela: o controller os monta, e só se o tópico não está bloqueado.</param>
    public static TopicResponse From(
        TopicView topic, Func<TopicLessonView, IReadOnlyList<Link>> lessonLinks, IReadOnlyList<Link> links) =>
        new(
            topic.Id,
            topic.Slug,
            topic.Name,
            TopicStatusText.Of(topic.Status),
            topic.Score,
            topic.AttemptCount,
            topic.MasteryThreshold,
            topic.MinAttempts,
            [.. topic.Prerequisites.Select(prerequisite =>
                new TopicPrerequisiteResponse(prerequisite.Id, prerequisite.Name, TopicStatusText.Of(prerequisite.Status)))],
            [.. topic.Unlocks.Select(unlocked => new TopicRefResponse(unlocked.Id, unlocked.Name))],
            [.. topic.Lessons.Select(lesson => new TopicLessonResponse(
                lesson.Id,
                lesson.Order,
                lesson.Title,
                LessonStatusText.Of(lesson.Status),
                lesson.CompletedExercises,
                lesson.TotalExercises,
                lessonLinks(lesson)))],
            links);
}
