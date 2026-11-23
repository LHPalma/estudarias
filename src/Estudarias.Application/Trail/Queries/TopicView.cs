using Estudarias.Domain.Students;

namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// O tópico para o aluno: a nota, as tentativas, o que falta para dominar (a meta e o mínimo de tentativas), os pré-requisitos com o
/// status de cada um para ele, o que o tópico libera e as lições. Um tópico bloqueado também é lido, para a tela mostrar o que falta.
/// </summary>
/// <param name="Score">Com uma casa, arredondada para baixo; nula enquanto o aluno não respondeu nada no tópico.</param>
/// <param name="AttemptCount">
/// Todas as tentativas do aluno no tópico, e não só as da janela da nota (PEND-TRL-01): é o que o domínio compara com
/// <paramref name="MinAttempts"/>.
/// </param>
public sealed record TopicView(
    Guid Id,
    string Slug,
    string Name,
    TopicStatus Status,
    decimal? Score,
    int AttemptCount,
    decimal MasteryThreshold,
    int MinAttempts,
    IReadOnlyList<TopicPrerequisiteView> Prerequisites,
    IReadOnlyList<TopicRefView> Unlocks,
    IReadOnlyList<TopicLessonView> Lessons);

/// <summary>Um pré-requisito, com o status dele para o aluno (o que falta dominar).</summary>
public sealed record TopicPrerequisiteView(Guid Id, string Name, TopicStatus Status);

/// <summary>Um tópico citado por outro: só o id e o nome.</summary>
public sealed record TopicRefView(Guid Id, string Name);

/// <summary>Uma lição do tópico, sem o conteúdo: este só sai na leitura da lição.</summary>
public sealed record TopicLessonView(Guid Id, int Order, string Title);
