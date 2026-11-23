using Estudarias.Domain.Exercises;

namespace Estudarias.Api.Contracts;

/// <summary>O texto da dificuldade no contrato: <c>easy</c>, <c>medium</c> ou <c>hard</c>. Explícito, como o do status do tópico.</summary>
internal static class DifficultyText
{
    public static string Of(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "easy",
        Difficulty.Medium => "medium",
        Difficulty.Hard => "hard",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty is not valid."),
    };
}
