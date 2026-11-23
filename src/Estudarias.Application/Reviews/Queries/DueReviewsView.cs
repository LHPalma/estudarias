namespace Estudarias.Application.Reviews.Queries;

/// <summary>
/// As revisões do dia: os cartões do aluno com a próxima revisão vencida, do mais atrasado para o mais recente. Cada cartão já traz a
/// pergunta e a resposta: virar o cartão é coisa do app, e a revisão funciona sem uma segunda chamada.
/// </summary>
public sealed record DueReviewsView(int Count, IReadOnlyList<DueCardView> Items);

/// <summary>Um cartão vencido. <c>Id</c> é o do cartão do aluno, o que a revisão usa.</summary>
public sealed record DueCardView(
    Guid Id, string Question, string Answer, Guid TopicId, string TopicName, DateTimeOffset NextReviewAt);
