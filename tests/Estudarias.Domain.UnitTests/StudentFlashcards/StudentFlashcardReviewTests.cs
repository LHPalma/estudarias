using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Domain.UnitTests.StudentFlashcards;

public class StudentFlashcardReviewTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Um scheduler escrito à mão que registra a chamada e devolve o que o teste mandar.</summary>
    private sealed class RecordingScheduler(string algorithm = "fake") : IReviewScheduler
    {
        public string Algorithm => algorithm;

        public (string State, ReviewRating Rating, DateTimeOffset ReviewedAt)? LastReview { get; private set; }

        public Exception? Failure { get; init; }

        public TimeSpan NextReviewIn { get; init; } = TimeSpan.FromDays(3);

        public ReviewOutcome Start(DateTimeOffset now) => new("state-0", now.AddDays(1));

        public ReviewOutcome Review(string state, ReviewRating rating, DateTimeOffset reviewedAt)
        {
            LastReview = (state, rating, reviewedAt);
            if (Failure is not null)
            {
                throw Failure;
            }

            return new ReviewOutcome($"{state}+{rating}", reviewedAt + NextReviewIn);
        }
    }

    private static StudentFlashcard NewCard(IReviewScheduler scheduler, TimeProvider clock) =>
        StudentFlashcard.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, scheduler, clock);

    [Fact(DisplayName = "Revisar delega ao scheduler com o estado atual, a nota e o instante do relógio")]
    public void Review_DelegatesWithCurrentStateRatingAndInstant()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new RecordingScheduler();
        var card = NewCard(scheduler, clock);
        clock.Advance(TimeSpan.FromDays(1));

        // Act
        card.Review(ReviewRating.Good, scheduler, clock);

        // Assert
        Assert.Equal(("state-0", ReviewRating.Good, Start.AddDays(1)), scheduler.LastReview);
    }

    [Fact(DisplayName = "Revisar grava o estado e a próxima revisão devolvidos pelo scheduler")]
    public void Review_StoresSchedulerOutcome()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new RecordingScheduler { NextReviewIn = TimeSpan.FromDays(3) };
        var card = NewCard(scheduler, clock);
        clock.Advance(TimeSpan.FromDays(1));

        // Act
        card.Review(ReviewRating.Hard, scheduler, clock);

        // Assert
        Assert.Equal("state-0+Hard", card.SchedulingState);
        Assert.Equal(Start.AddDays(4), card.NextReviewAt);
    }

    [Fact(DisplayName = "Uma segunda revisão parte do estado gravado pela primeira")]
    public void Review_Twice_ChainsState()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new RecordingScheduler();
        var card = NewCard(scheduler, clock);

        // Act
        card.Review(ReviewRating.Good, scheduler, clock);
        card.Review(ReviewRating.Easy, scheduler, clock);

        // Assert
        Assert.Equal("state-0+Good+Easy", card.SchedulingState);
    }

    [Fact(DisplayName = "Um scheduler de outro algoritmo é recusado sem mudar nada")]
    public void Review_WithSchedulerOfAnotherAlgorithm_ThrowsAndKeepsState()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var card = NewCard(new RecordingScheduler("fake"), clock);
        var other = new RecordingScheduler("other");

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => card.Review(ReviewRating.Good, other, clock));
        Assert.Equal("studentFlashcard.scheduler.mismatch", exception.Code);
        Assert.Null(other.LastReview);
        Assert.Equal("state-0", card.SchedulingState);
        Assert.Equal(Start.AddDays(1), card.NextReviewAt);
    }

    [Theory(DisplayName = "Uma nota desconhecida é recusada sem mudar nada")]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void Review_WithUndefinedRating_ThrowsAndKeepsState(int rating)
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new RecordingScheduler();
        var card = NewCard(scheduler, clock);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => card.Review((ReviewRating)rating, scheduler, clock));
        Assert.Equal("studentFlashcard.rating.invalid", exception.Code);
        Assert.Null(scheduler.LastReview);
        Assert.Equal("state-0", card.SchedulingState);
        Assert.Equal(Start.AddDays(1), card.NextReviewAt);
    }

    [Fact(DisplayName = "Se o scheduler lançar exceção, o cartão fica intacto")]
    public void Review_WhenSchedulerThrows_KeepsState()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var card = NewCard(new RecordingScheduler(), clock);
        var failing = new RecordingScheduler { Failure = new InvalidOperationException("estado corrompido") };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => card.Review(ReviewRating.Good, failing, clock));
        Assert.Equal("state-0", card.SchedulingState);
        Assert.Equal(Start.AddDays(1), card.NextReviewAt);
    }

    [Theory(DisplayName = "Uma revisão adiantada ou atrasada reagenda a partir do momento real")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void Review_EarlyOrLate_SchedulesFromActualInstant(int daysAfterStart)
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new RecordingScheduler { NextReviewIn = TimeSpan.FromDays(3) };
        var card = NewCard(scheduler, clock);
        clock.Advance(TimeSpan.FromDays(daysAfterStart));

        // Act
        card.Review(ReviewRating.Good, scheduler, clock);

        // Assert
        Assert.Equal(Start.AddDays(daysAfterStart + 3), card.NextReviewAt);
    }

    [Fact(DisplayName = "Com o SM-2 real, acertos seguidos avançam 1, 6 e depois intervalo × fator")]
    public void Review_WithSm2_ConsecutiveGoodReviewsFollowSm2Intervals()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new Sm2Scheduler();
        var card = NewCard(scheduler, clock);
        var intervals = new List<double>();

        // Act
        for (var review = 0; review < 3; review++)
        {
            clock.SetUtcNow(card.NextReviewAt);
            card.Review(ReviewRating.Good, scheduler, clock);
            intervals.Add((card.NextReviewAt - clock.GetUtcNow()).TotalDays);
        }

        // Assert
        Assert.Equal([1d, 6d, 15d], intervals);
    }

    [Fact(DisplayName = "Com o SM-2 real, uma falha reinicia o intervalo para 1 dia")]
    public void Review_WithSm2_FailureResetsToOneDay()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new Sm2Scheduler();
        var card = NewCard(scheduler, clock);
        card.Review(ReviewRating.Good, scheduler, clock);
        card.Review(ReviewRating.Good, scheduler, clock);
        clock.Advance(TimeSpan.FromDays(6));

        // Act
        card.Review(ReviewRating.Again, scheduler, clock);

        // Assert
        Assert.Equal(clock.GetUtcNow().AddDays(1), card.NextReviewAt);
        Assert.Equal(0, Sm2Scheduler.ReadState(card.SchedulingState).Repetitions);
    }

    [Fact(DisplayName = "Com o SM-2 real, o cartão deixa de estar vencido logo depois da revisão")]
    public void Review_WithSm2_CardIsNoLongerDue()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new Sm2Scheduler();
        var card = NewCard(scheduler, clock);
        clock.Advance(TimeSpan.FromDays(2));
        Assert.True(card.IsDue(clock));

        // Act
        card.Review(ReviewRating.Good, scheduler, clock);

        // Assert
        Assert.False(card.IsDue(clock));
    }
}
