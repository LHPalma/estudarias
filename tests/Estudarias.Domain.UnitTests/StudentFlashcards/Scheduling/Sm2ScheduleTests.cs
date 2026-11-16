using Estudarias.Domain.StudentFlashcards.Scheduling;

namespace Estudarias.Domain.UnitTests.StudentFlashcards.Scheduling;

public class Sm2ScheduleTests
{
    [Fact(DisplayName = "O estado inicial tem 0 repetições, 1 dia e fator 2,5")]
    public void Initial_HasZeroRepetitionsOneDayAndDefaultEaseFactor()
    {
        // Act
        var initial = Sm2Schedule.Initial;

        // Assert
        Assert.Equal(new Sm2Schedule(0, 1, 2.5m), initial);
    }

    [Fact(DisplayName = "Acertos seguidos com nota 4 levam o intervalo a 1, 6 e depois a intervalo × fator")]
    public void Next_WithConsecutiveGoodReviews_FollowsSm2Intervals()
    {
        // Arrange
        var schedule = Sm2Schedule.Initial;
        var intervals = new List<int>();

        // Act
        for (var review = 0; review < 5; review++)
        {
            schedule = schedule.Next(4);
            intervals.Add(schedule.IntervalDays);
        }

        // Assert
        // 15 × 2,5 = 37,5 → 38; 38 × 2,5 = 95.
        Assert.Equal([1, 6, 15, 38, 95], intervals);
        Assert.Equal(5, schedule.Repetitions);
        Assert.Equal(2.5m, schedule.EaseFactor);
    }

    [Fact(DisplayName = "O intervalo usa o fator de antes da atualização")]
    public void Next_UsesEaseFactorBeforeUpdate()
    {
        // Arrange
        var schedule = Sm2Schedule.Initial.Next(5).Next(5);

        // Act
        var next = schedule.Next(5);

        // Assert
        // Antes: 2 repetições, 6 dias e fator 2,7. O intervalo é 6 × 2,7 = 16,2 → 16, e o fator passa a 2,8.
        Assert.Equal(new Sm2Schedule(2, 6, 2.7m), schedule);
        Assert.Equal(new Sm2Schedule(3, 16, 2.8m), next);
    }

    [Theory(DisplayName = "Cada nota de acerto muda o fator conforme a fórmula")]
    [InlineData(5, 2.6)]
    [InlineData(4, 2.5)]
    [InlineData(3, 2.36)]
    public void Next_WithPassingQuality_UpdatesEaseFactor(int quality, double expected)
    {
        // Act
        var next = Sm2Schedule.Initial.Next(quality);

        // Assert
        Assert.Equal(new Sm2Schedule(1, 1, (decimal)expected), next);
    }

    [Theory(DisplayName = "Uma nota abaixo de 3 zera as repetições e o intervalo sem mexer no fator")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Next_WithFailingQuality_ResetsWithoutChangingEaseFactor(int quality)
    {
        // Arrange
        var schedule = new Sm2Schedule(4, 38, 2.36m);

        // Act
        var next = schedule.Next(quality);

        // Assert
        Assert.Equal(new Sm2Schedule(0, 1, 2.36m), next);
    }

    [Fact(DisplayName = "Depois de uma falha, o próximo acerto recomeça em 1 dia")]
    public void Next_AfterFailure_RestartsSequence()
    {
        // Arrange
        var failed = new Sm2Schedule(4, 38, 2.5m).Next(1);

        // Act
        var first = failed.Next(4);
        var second = first.Next(4);

        // Assert
        Assert.Equal(1, first.IntervalDays);
        Assert.Equal(6, second.IntervalDays);
    }

    [Fact(DisplayName = "O fator nunca fica abaixo de 1,3")]
    public void Next_NeverDropsEaseFactorBelowMinimum()
    {
        // Arrange
        var schedule = new Sm2Schedule(3, 10, 1.4m);

        // Act
        var next = schedule.Next(3);
        var again = next.Next(3);

        // Assert
        // 1,4 − 0,14 = 1,26, que fica em 1,3; e continua em 1,3.
        Assert.Equal(new Sm2Schedule(4, 14, 1.3m), next);
        Assert.Equal(1.3m, again.EaseFactor);
    }

    [Theory(DisplayName = "O intervalo é arredondado para o inteiro mais próximo, com o meio para longe do zero")]
    [InlineData(5, 2.5, 13)]
    [InlineData(6, 2.36, 14)]
    [InlineData(6, 2.42, 15)]
    public void Next_RoundsIntervalAwayFromZero(int interval, double easeFactor, int expected)
    {
        // Arrange
        var schedule = new Sm2Schedule(2, interval, (decimal)easeFactor);

        // Act
        var next = schedule.Next(4);

        // Assert
        Assert.Equal(expected, next.IntervalDays);
    }

    [Fact(DisplayName = "O fator é arredondado para 2 casas, com o meio para longe do zero")]
    public void Next_RoundsEaseFactorToTwoPlaces()
    {
        // Arrange
        var schedule = new Sm2Schedule(0, 1, 2.565m);

        // Act
        var next = schedule.Next(5);

        // Assert
        // 2,565 + 0,1 = 2,665 → 2,67.
        Assert.Equal(2.67m, next.EaseFactor);
    }

    [Theory(DisplayName = "Uma nota fora de 0 a 5 lança ArgumentOutOfRangeException")]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void Next_WithQualityOutOfRange_ThrowsArgumentOutOfRangeException(int quality)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Sm2Schedule.Initial.Next(quality));
    }
}
