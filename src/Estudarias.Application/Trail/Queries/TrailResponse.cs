using Estudarias.Domain.Students;

namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// A trilha do aluno: os tópicos em ordem de pré-requisito, com o status e a nota de cada um, e os totais que a tela mostra no
/// topo. <paramref name="MasteryThreshold"/> é a meta de domínio, para o app desenhar a barra sem repetir o número.
/// </summary>
public sealed record TrailResponse(
    decimal MasteryThreshold,
    int MasteredTopics,
    int TotalTopics,
    int DueReviews,
    IReadOnlyList<TrailTopicResponse> Topics);

/// <param name="Score">
/// De 0 a 100, com uma casa, arredondada para baixo (<c>TopicScore.Display</c>); nula enquanto o aluno não respondeu nada no tópico.
/// </param>
/// <param name="Prerequisites">Os ids dos tópicos exigidos, para o mapa desenhar o grafo.</param>
public sealed record TrailTopicResponse(
    Guid Id,
    string Slug,
    string Name,
    TopicStatus Status,
    decimal? Score,
    IReadOnlyList<Guid> Prerequisites);
