using Estudarias.Domain.Exercises;
using Estudarias.Domain.Scoring;

namespace Estudarias.Domain.UnitTests.Scoring;

public class TopicScoreTests
{
    private static GradedAnswer Right(Difficulty difficulty) => new(difficulty, IsCorrect: true);

    private static GradedAnswer Wrong(Difficulty difficulty) => new(difficulty, IsCorrect: false);

    [Fact(DisplayName = "Sem respostas, a nota é 0")]
    public void Compute_WithoutAnswers_IsZero()
    {
        // Act
        var score = TopicScore.Compute([]);

        // Assert
        Assert.Equal(0m, score.Value);
        Assert.Equal(TopicScore.Zero, score);
    }

    [Fact(DisplayName = "Só acertos dá nota 100")]
    public void Compute_WithOnlyRightAnswers_Is100()
    {
        // Act
        var score = TopicScore.Compute([Right(Difficulty.Easy), Right(Difficulty.Hard), Right(Difficulty.Medium)]);

        // Assert
        Assert.Equal(100m, score.Value);
    }

    [Fact(DisplayName = "Só erros dá nota 0")]
    public void Compute_WithOnlyWrongAnswers_IsZero()
    {
        // Act
        var score = TopicScore.Compute([Wrong(Difficulty.Easy), Wrong(Difficulty.Hard), Wrong(Difficulty.Medium)]);

        // Assert
        Assert.Equal(0m, score.Value);
    }

    [Fact(DisplayName = "Um acerto difícil pesa mais que um erro fácil")]
    public void Compute_HardRightAndEasyWrong_IsAboveHalf()
    {
        // Act
        var score = TopicScore.Compute([Right(Difficulty.Hard), Wrong(Difficulty.Easy)]);

        // Assert
        Assert.True(score.Value > 50m);
    }

    [Fact(DisplayName = "Um erro difícil pesa mais que um acerto fácil")]
    public void Compute_EasyRightAndHardWrong_IsBelowHalf()
    {
        // Act
        var score = TopicScore.Compute([Right(Difficulty.Easy), Wrong(Difficulty.Hard)]);

        // Assert
        Assert.True(score.Value < 50m);
    }

    [Fact(DisplayName = "A resposta mais recente pesa mais que a mais antiga, com a mesma dificuldade")]
    public void Compute_MostRecentAnswer_WeighsMore()
    {
        // Act
        var recentRight = TopicScore.Compute([Right(Difficulty.Medium), Wrong(Difficulty.Medium)]);
        var recentWrong = TopicScore.Compute([Wrong(Difficulty.Medium), Right(Difficulty.Medium)]);

        // Assert
        Assert.True(recentRight.Value > 50m);
        Assert.True(recentWrong.Value < 50m);
    }

    [Fact(DisplayName = "Respostas depois da 10ª não mudam a nota")]
    public void Compute_IgnoresAnswersBeyondWindow()
    {
        // Arrange
        var tenRight = Enumerable.Repeat(Right(Difficulty.Medium), TopicScore.Window).ToList();
        var withOlderWrongs = tenRight.Concat(Enumerable.Repeat(Wrong(Difficulty.Hard), 5)).ToList();

        // Act
        var score = TopicScore.Compute(withOlderWrongs);

        // Assert
        Assert.Equal(100m, score.Value);
    }

    [Fact(DisplayName = "A 10ª resposta ainda conta para a nota")]
    public void Compute_CountsTenthAnswer()
    {
        // Arrange
        var answers = Enumerable.Repeat(Right(Difficulty.Medium), TopicScore.Window - 1)
            .Append(Wrong(Difficulty.Medium))
            .ToList();

        // Act
        var score = TopicScore.Compute(answers);

        // Assert
        Assert.True(score.Value < 100m);
    }

    [Fact(DisplayName = "A nota segue a fórmula com as três dificuldades (caso calculado à mão)")]
    public void Compute_MatchesHandCalculatedCase()
    {
        // Arrange
        // Pesos: difícil 3 × 0,9⁰ = 3; fácil 1 × 0,9¹ = 0,9; médio 2 × 0,9² = 1,62.
        // Nota = 100 × (3 + 1,62) / (3 + 0,9 + 1,62) = 100 × 4,62 / 5,52 ≈ 83,6957.
        GradedAnswer[] answers = [Right(Difficulty.Hard), Wrong(Difficulty.Easy), Right(Difficulty.Medium)];

        // Act
        var score = TopicScore.Compute(answers);

        // Assert
        Assert.Equal(83.6957m, Math.Round(score.Value, 4));
    }

    [Fact(DisplayName = "Uma dificuldade inválida lança ArgumentOutOfRangeException")]
    public void Compute_WithUndefinedDifficulty_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => TopicScore.Compute([Right((Difficulty)99)]));
    }

    [Fact(DisplayName = "Uma lista nula lança ArgumentNullException")]
    public void Compute_WithNullList_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TopicScore.Compute(null!));
    }
}
