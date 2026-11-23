using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Reviews;
using Estudarias.Application.StudentFlashcards;
using Estudarias.Application.UnitTests.Support;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Estudarias.Application.UnitTests.Reviews;

public class ReviewFlashcardHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IStudentFlashcardRepository _cards = Substitute.For<IStudentFlashcardRepository>();
    private readonly IReviewSchedulers _schedulers = Substitute.For<IReviewSchedulers>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start);
    private readonly Sm2Scheduler _sm2 = new();

    private readonly User _user;
    private readonly StudentFlashcard _card;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ReviewFlashcardHandlerTests()
    {
        var studentId = Guid.CreateVersion7();
        _user = User.RegisterStudent("ana@example.com", TestUsers.Hash, studentId);
        _card = StudentFlashcard.Create(studentId, Guid.CreateVersion7(), null, _sm2, _clock);

        _users.FindByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _cards.FindAsync(_card.Id, studentId, Arg.Any<CancellationToken>()).Returns(_card);
        _schedulers.For(Sm2Scheduler.AlgorithmName).Returns(_sm2);
    }

    private ReviewFlashcardHandler Handler() => new(_users, _cards, _schedulers, _unitOfWork, _clock);

    private Task<ReviewFlashcardResult> ReviewAsync(ReviewRating? rating, Guid? cardId = null) =>
        Handler().Handle(_user.Id, new ReviewFlashcardCommand(cardId ?? _card.Id, rating), Cancel);

    [Fact(DisplayName = "Revisar reagenda o cartão a partir do instante da revisão e grava uma vez")]
    public async Task Handle_ReschedulesTheCardFromTheInstantOfTheReview()
    {
        // Arrange: o aluno revisa três dias depois de o cartão ter nascido, bem depois do vencimento
        _clock.Advance(TimeSpan.FromDays(3));

        // Act
        var result = await ReviewAsync(ReviewRating.Good);

        // Assert: a primeira revisão correta do SM-2 mantém 1 dia, contado do instante real da revisão (RN-REV-08)
        Assert.Equal((_card.Id, Start.AddDays(3).AddDays(1)), (result.Id, result.NextReviewAt));
        Assert.Equal(result.NextReviewAt, _card.NextReviewAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
    }

    [Theory(DisplayName = "Cada uma das quatro notas é aceita")]
    [InlineData(ReviewRating.Again)]
    [InlineData(ReviewRating.Hard)]
    [InlineData(ReviewRating.Good)]
    [InlineData(ReviewRating.Easy)]
    public async Task Handle_AcceptsEachRating(ReviewRating rating)
    {
        // Act
        var result = await ReviewAsync(rating);

        // Assert
        Assert.True(result.NextReviewAt > Start);
    }

    [Fact(DisplayName = "O agendador é o do algoritmo gravado no cartão")]
    public async Task Handle_UsesTheSchedulerOfTheCardAlgorithm()
    {
        // Act
        await ReviewAsync(ReviewRating.Good);

        // Assert
        _schedulers.Received(1).For("sm2");
    }

    [Fact(DisplayName = "Revisar antes do vencimento é aceito")]
    public async Task Handle_BeforeTheDueDate_IsAccepted()
    {
        // Arrange: o cartão novo só vence daqui a um dia, e a revisão chega logo depois de criado
        Assert.False(_card.IsDue(_clock));

        // Act
        var result = await ReviewAsync(ReviewRating.Easy);

        // Assert
        Assert.Equal(result.NextReviewAt, _card.NextReviewAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "RN-REV-11: o cartão de outro aluno é review.card.notFound, como um que não existe, e nada é gravado")]
    public async Task Handle_WithTheCardOfAnotherStudent_ThrowsNotFound()
    {
        // Arrange: o repositório só devolve o cartão para o dono dele
        var other = User.RegisterStudent("bia@example.com", TestUsers.Hash, Guid.CreateVersion7());
        _users.FindByIdAsync(other.Id, Arg.Any<CancellationToken>()).Returns(other);

        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() =>
            Handler().Handle(other.Id, new ReviewFlashcardCommand(_card.Id, ReviewRating.Good), Cancel));

        // Assert
        Assert.Equal(ReviewErrors.CardNotFound, exception.Code);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um cartão que não existe é review.card.notFound")]
    public async Task Handle_WithAnUnknownCard_ThrowsNotFound()
    {
        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() => ReviewAsync(ReviewRating.Good, Guid.CreateVersion7()));

        // Assert
        Assert.Equal(ReviewErrors.CardNotFound, exception.Code);
    }

    [Theory(DisplayName = "FA-REV-03: uma nota ausente ou fora do enum é studentFlashcard.rating.invalid, sem tocar o banco")]
    [InlineData(null)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task Handle_WithAnInvalidRating_ThrowsBeforeReadingAnything(int? rating)
    {
        // Act
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => ReviewAsync(rating is null ? null : (ReviewRating)rating));

        // Assert
        Assert.Equal(StudentFlashcardErrors.RatingInvalid, exception.Code);
        await _users.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FA-REV-06: um algoritmo sem agendador é erro de configuração, e o cartão não é gravado")]
    public async Task Handle_WithAnAlgorithmWithoutAScheduler_Throws()
    {
        // Arrange
        _schedulers.For(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("No review scheduler is registered."));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReviewAsync(ReviewRating.Good));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Uma conta desconhecida, desabilitada ou sem aluno é sessão inválida")]
    public async Task Handle_ForAnInvalidAccount_ThrowsInvalidSession()
    {
        // Arrange
        var unknown = Guid.CreateVersion7();
        _users.FindByIdAsync(unknown, Arg.Any<CancellationToken>()).Returns((User?)null);
        var inactive = TestUsers.Inactive();
        _users.FindByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);

        // Act & Assert
        foreach (var userId in new[] { unknown, inactive.Id })
        {
            var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() =>
                Handler().Handle(userId, new ReviewFlashcardCommand(_card.Id, ReviewRating.Good), Cancel));
            Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
        }
    }

    [Fact(DisplayName = "FA-REV-08: duas revisões seguidas do mesmo cartão com a mesma nota levam ao mesmo estado (a última gravação vale)")]
    public void TwoReviewsWithTheSameRating_LeaveTheSameState()
    {
        // Arrange: dois cartões iguais, lidos antes de qualquer revisão, como duas requisições simultâneas
        var studentId = _user.StudentId!.Value;
        var first = StudentFlashcard.Create(studentId, Guid.CreateVersion7(), null, _sm2, _clock);
        var second = StudentFlashcard.Create(studentId, first.FlashcardId, null, _sm2, _clock);

        // Act
        first.Review(ReviewRating.Good, _sm2, _clock);
        second.Review(ReviewRating.Good, _sm2, _clock);

        // Assert
        Assert.Equal((first.SchedulingState, first.NextReviewAt), (second.SchedulingState, second.NextReviewAt));
    }
}
