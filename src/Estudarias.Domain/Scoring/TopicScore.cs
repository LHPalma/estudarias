using Estudarias.Domain.Exercises;

namespace Estudarias.Domain.Scoring;

/// <summary>
/// A nota de 0 a 100 de um aluno em um tópico: média das últimas respostas, ponderada pela dificuldade e pela recência.
/// </summary>
public readonly record struct TopicScore
{
    public const int Window = 10;
    public const decimal Decay = 0.9m;
    public const decimal Max = 100m;

    private TopicScore(decimal value) => Value = value;

    public decimal Value { get; }

    public static TopicScore Zero => default;

    /// <param name="mostRecentFirst">As respostas do aluno no tópico, da mais recente para a mais antiga.</param>
    public static TopicScore Compute(IReadOnlyList<GradedAnswer> mostRecentFirst)
    {
        ArgumentNullException.ThrowIfNull(mostRecentFirst);

        var earned = 0m;
        var total = 0m;
        var recency = 1m;
        var count = Math.Min(mostRecentFirst.Count, Window);

        for (var position = 0; position < count; position++)
        {
            var answer = mostRecentFirst[position];
            var weight = WeightOf(answer.Difficulty) * recency;

            total += weight;
            if (answer.IsCorrect)
            {
                earned += weight;
            }

            recency *= Decay;
        }

        return total == 0m ? Zero : new TopicScore(Max * earned / total);
    }

    private static int WeightOf(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 1,
        Difficulty.Medium => 2,
        Difficulty.Hard => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty is not valid."),
    };
}
