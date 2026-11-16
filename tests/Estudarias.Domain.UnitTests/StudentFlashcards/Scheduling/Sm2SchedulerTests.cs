using Estudarias.Domain.StudentFlashcards.Scheduling;

namespace Estudarias.Domain.UnitTests.StudentFlashcards.Scheduling;

public class Sm2SchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly Sm2Scheduler _scheduler = new();

    [Fact(DisplayName = "O algoritmo do adapter se chama sm2")]
    public void Algorithm_IsSm2()
    {
        // Assert
        Assert.Equal("sm2", _scheduler.Algorithm);
    }

    [Fact(DisplayName = "Começar um cartão grava o estado inicial e agenda a primeira revisão para 1 dia depois")]
    public void Start_StoresInitialStateAndSchedulesOneDayLater()
    {
        // Act
        var outcome = _scheduler.Start(Now);

        // Assert
        Assert.Equal(Sm2Schedule.Initial, Sm2Scheduler.ReadState(outcome.State));
        Assert.Equal(Now.AddDays(1), outcome.NextReviewAt);
    }

    [Theory(DisplayName = "Cada nota neutra vira a nota do SM-2: Again 1, Hard 3, Good 4, Easy 5")]
    [InlineData(ReviewRating.Again, 1)]
    [InlineData(ReviewRating.Hard, 3)]
    [InlineData(ReviewRating.Good, 4)]
    [InlineData(ReviewRating.Easy, 5)]
    public void QualityOf_MapsEachRating(ReviewRating rating, int expected)
    {
        // Act & Assert
        Assert.Equal(expected, Sm2Scheduler.QualityOf(rating));
    }

    [Theory(DisplayName = "Revisar aplica o SM-2 com a nota traduzida")]
    [InlineData(ReviewRating.Again, 0, 1, 2.5)]
    [InlineData(ReviewRating.Hard, 3, 15, 2.36)]
    [InlineData(ReviewRating.Good, 3, 15, 2.5)]
    [InlineData(ReviewRating.Easy, 3, 15, 2.6)]
    public void Review_AppliesSm2WithMappedQuality(
        ReviewRating rating, int repetitions, int intervalDays, double easeFactor)
    {
        // Arrange
        var state = Sm2Scheduler.WriteState(new Sm2Schedule(2, 6, 2.5m));

        // Act
        var outcome = _scheduler.Review(state, rating, Now);

        // Assert
        Assert.Equal(
            new Sm2Schedule(repetitions, intervalDays, (decimal)easeFactor),
            Sm2Scheduler.ReadState(outcome.State));
    }

    [Theory(DisplayName = "A próxima revisão é agendada a partir do instante real da revisão")]
    [InlineData(-3)]
    [InlineData(0)]
    [InlineData(10)]
    public void Review_SchedulesFromReviewInstant(int daysFromNow)
    {
        // Arrange
        var state = Sm2Scheduler.WriteState(new Sm2Schedule(1, 1, 2.5m));
        var reviewedAt = Now.AddDays(daysFromNow);

        // Act
        var outcome = _scheduler.Review(state, ReviewRating.Good, reviewedAt);

        // Assert
        Assert.Equal(reviewedAt.AddDays(6), outcome.NextReviewAt);
    }

    [Fact(DisplayName = "O estado vai e volta do JSON sem perder nada")]
    public void State_RoundTrips()
    {
        // Arrange
        var schedule = new Sm2Schedule(7, 123, 1.94m);

        // Act
        var read = Sm2Scheduler.ReadState(Sm2Scheduler.WriteState(schedule));

        // Assert
        Assert.Equal(schedule, read);
    }

    [Fact(DisplayName = "O estado é gravado em JSON com os nomes do schema")]
    public void WriteState_UsesCamelCaseJson()
    {
        // Act
        var state = Sm2Scheduler.WriteState(Sm2Schedule.Initial);

        // Assert
        Assert.Equal("""{"repetitions":0,"intervalDays":1,"easeFactor":2.5}""", state);
    }

    [Theory(DisplayName = "Um estado ilegível lança InvalidOperationException")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("não é json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"repetitions":0,"intervalDays":1}""")]
    [InlineData("""{"repetitions":"zero","intervalDays":1,"easeFactor":2.5}""")]
    [InlineData("""{"repetitions":-1,"intervalDays":1,"easeFactor":2.5}""")]
    [InlineData("""{"repetitions":0,"intervalDays":0,"easeFactor":2.5}""")]
    [InlineData("""{"repetitions":0,"intervalDays":1,"easeFactor":1.2}""")]
    public void Review_WithUnreadableState_ThrowsInvalidOperationException(string state)
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _scheduler.Review(state, ReviewRating.Good, Now));
    }

    [Fact(DisplayName = "Uma nota neutra desconhecida lança ArgumentOutOfRangeException")]
    public void QualityOf_WithUndefinedRating_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Sm2Scheduler.QualityOf((ReviewRating)42));
    }
}
