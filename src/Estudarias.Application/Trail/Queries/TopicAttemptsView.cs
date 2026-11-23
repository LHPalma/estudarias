using Estudarias.Domain.Exercises;

namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// Uma resposta do aluno no tópico: o exercício (id e enunciado) e se acertou, da janela que compõe a nota. Mostra ao aluno por que a
/// nota subiu ou caiu, e nada além disso: não traz a alternativa escolhida, nem a correta.
/// </summary>
public sealed record TopicAttemptView(
    Guid AttemptId,
    Guid ExerciseId,
    string Statement,
    Difficulty Difficulty,
    bool IsCorrect,
    DateTimeOffset AnsweredAt);
