using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Domain.UnitTests.StudentFlashcards;

public class StudentFlashcardTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid StudentId = Guid.CreateVersion7();
    private static readonly Guid FlashcardId = Guid.CreateVersion7();
    private static readonly Guid AttemptId = Guid.CreateVersion7();

    /// <summary>Um scheduler escrito à mão, para isolar o agregado do SM-2.</summary>
    private sealed class FakeScheduler(TimeSpan firstReview) : IReviewScheduler
    {
        public string Algorithm => "fake";

        public DateTimeOffset? StartedAt { get; private set; }

        public ReviewOutcome Start(DateTimeOffset now)
        {
            StartedAt = now;
            return new ReviewOutcome("""{"fake":true}""", now + firstReview);
        }

        public ReviewOutcome Review(string state, ReviewRating rating, DateTimeOffset reviewedAt) =>
            throw new NotSupportedException();
    }

    private static StudentFlashcard NewCard(FakeTimeProvider clock, TimeSpan? firstReview = null) =>
        StudentFlashcard.Create(StudentId, FlashcardId, AttemptId, new FakeScheduler(firstReview ?? TimeSpan.FromDays(2)), clock);

    [Fact(DisplayName = "Um cartão do aluno nasce com o aluno, o cartão, a tentativa de origem e o que o scheduler devolveu")]
    public void Create_KeepsIdsAndSchedulerOutcome()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var scheduler = new FakeScheduler(TimeSpan.FromDays(2));

        // Act
        var card = StudentFlashcard.Create(StudentId, FlashcardId, AttemptId, scheduler, clock);

        // Assert
        Assert.Equal(StudentId, card.StudentId);
        Assert.Equal(FlashcardId, card.FlashcardId);
        Assert.Equal(AttemptId, card.OriginAttemptId);
        Assert.Equal("fake", card.Algorithm);
        Assert.Equal("""{"fake":true}""", card.SchedulingState);
        Assert.Equal(Start.AddDays(2), card.NextReviewAt);
        Assert.Equal(Start, scheduler.StartedAt);
    }

    [Fact(DisplayName = "A tentativa de origem é opcional")]
    public void Create_WithoutOriginAttempt_StoresNull()
    {
        // Act
        var card = StudentFlashcard.Create(
            StudentId, FlashcardId, null, new FakeScheduler(TimeSpan.FromDays(1)), new FakeTimeProvider(Start));

        // Assert
        Assert.Null(card.OriginAttemptId);
    }

    [Fact(DisplayName = "O aluno do cartão é obrigatório")]
    public void Create_WithEmptyStudent_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => StudentFlashcard.Create(
                Guid.Empty, FlashcardId, AttemptId, new FakeScheduler(TimeSpan.FromDays(1)), new FakeTimeProvider(Start)));
        Assert.Equal("studentFlashcard.student.required", exception.Code);
    }

    [Fact(DisplayName = "O flashcard do cartão é obrigatório")]
    public void Create_WithEmptyFlashcard_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => StudentFlashcard.Create(
                StudentId, Guid.Empty, AttemptId, new FakeScheduler(TimeSpan.FromDays(1)), new FakeTimeProvider(Start)));
        Assert.Equal("studentFlashcard.flashcard.required", exception.Code);
    }

    [Fact(DisplayName = "A tentativa de origem, se informada, não pode ser vazia")]
    public void Create_WithEmptyOriginAttempt_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => StudentFlashcard.Create(
                StudentId, FlashcardId, Guid.Empty, new FakeScheduler(TimeSpan.FromDays(1)), new FakeTimeProvider(Start)));
        Assert.Equal("studentFlashcard.originAttempt.invalid", exception.Code);
    }

    [Fact(DisplayName = "Antes da próxima revisão, o cartão não está vencido")]
    public void IsDue_BeforeNextReview_IsFalse()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var card = NewCard(clock, TimeSpan.FromDays(2));
        clock.Advance(TimeSpan.FromDays(2) - TimeSpan.FromSeconds(1));

        // Act & Assert
        Assert.False(card.IsDue(clock));
    }

    [Fact(DisplayName = "Exatamente no instante da próxima revisão, o cartão está vencido")]
    public void IsDue_AtNextReview_IsTrue()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var card = NewCard(clock, TimeSpan.FromDays(2));
        clock.Advance(TimeSpan.FromDays(2));

        // Act & Assert
        Assert.True(card.IsDue(clock));
    }

    [Fact(DisplayName = "Depois da próxima revisão, o cartão está vencido")]
    public void IsDue_AfterNextReview_IsTrue()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var card = NewCard(clock, TimeSpan.FromDays(2));
        clock.Advance(TimeSpan.FromDays(5));

        // Act & Assert
        Assert.True(card.IsDue(clock));
    }

    [Fact(DisplayName = "Cada cartão do aluno nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);

        // Act
        var first = NewCard(clock);
        var second = NewCard(clock);

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact(DisplayName = "Com o SM-2 real, a primeira revisão fica para 1 dia depois da criação")]
    public void Create_WithSm2Scheduler_SchedulesFirstReviewOneDayLater()
    {
        // Act
        var card = StudentFlashcard.Create(StudentId, FlashcardId, AttemptId, new Sm2Scheduler(), new FakeTimeProvider(Start));

        // Assert
        Assert.Equal("sm2", card.Algorithm);
        Assert.Equal(Start.AddDays(1), card.NextReviewAt);
        Assert.Equal(Sm2Schedule.Initial, Sm2Scheduler.ReadState(card.SchedulingState));
    }
}
