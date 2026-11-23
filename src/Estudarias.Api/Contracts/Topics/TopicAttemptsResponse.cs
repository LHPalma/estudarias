using Estudarias.Api.Links;
using Estudarias.Application.Trail.Queries;

namespace Estudarias.Api.Contracts.Topics;

/// <summary>
/// Uma resposta do aluno no tópico: o exercício, a dificuldade (<c>easy</c>, <c>medium</c> ou <c>hard</c>) e se acertou. Não traz a
/// alternativa escolhida nem a correta: o histórico explica a nota, e não revisa o exercício.
/// </summary>
public sealed record TopicAttemptResponse(
    Guid AttemptId,
    Guid ExerciseId,
    string Statement,
    string Difficulty,
    bool IsCorrect,
    DateTimeOffset AnsweredAt);

/// <summary>
/// O histórico do aluno logado num tópico (<c>GET /topics/{id}/attempts</c>): as respostas que entram na nota (as 10 mais recentes), da
/// mais recente para a mais antiga. Os <c>links</c> são montados pelo controller, que conhece as rotas (ADR-004).
/// </summary>
public sealed record TopicAttemptsResponse(IReadOnlyList<TopicAttemptResponse> Items, IReadOnlyList<Link> Links)
{
    public static TopicAttemptsResponse From(IReadOnlyList<TopicAttemptView> attempts, IReadOnlyList<Link> links) =>
        new(
            [.. attempts.Select(attempt => new TopicAttemptResponse(
                attempt.AttemptId,
                attempt.ExerciseId,
                attempt.Statement,
                DifficultyText.Of(attempt.Difficulty),
                attempt.IsCorrect,
                attempt.AnsweredAt))],
            links);
}
