using Estudarias.Application.Reviews;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Reviews;

/// <summary>Revisar um cartão (#89) pelo composition root e contra o PostgreSQL: o estado jsonb do SM-2, o 404 e os defeitos do servidor.</summary>
[Collection(PostgresCollection.Name)]
public class ReviewFlashcardFlowTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private static readonly Sm2Scheduler Scheduler = new();

    private async Task<(Guid UserId, Guid StudentId, StudentFlashcard Card)> StudentWithACardAsync(string email = "ana@example.com")
    {
        var content = await SeedContentAsync();
        var user = await SeedUserAsync(email);
        using var scope = NewScope();
        var card = StudentFlashcard.Create(user.StudentId!.Value, content.Generated.Id, null, Scheduler, Clock);
        scope.StudentFlashcards.Add(card);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
        return (user.Id, user.StudentId!.Value, card);
    }

    private async Task<ReviewFlashcardResult> ReviewAsync(Guid userId, Guid cardId, ReviewRating rating)
    {
        using var scope = NewScope();
        return await scope.Get<ReviewFlashcardHandler>().Handle(userId, new ReviewFlashcardCommand(cardId, rating), Cancel);
    }

    private Task<DateTimeOffset> StoredNextReviewAsync(Guid cardId) =>
        ScalarAsync<DateTime>($"SELECT next_review_at FROM student_flashcard WHERE id = '{cardId}'")
            .ContinueWith(task => new DateTimeOffset(DateTime.SpecifyKind(task.Result, DateTimeKind.Utc)), Cancel);

    [Fact(DisplayName = "Revisar grava o estado novo (jsonb) e a próxima revisão, que partem do instante da revisão")]
    public async Task Review_StoresTheNewStateAndTheNextReview()
    {
        // Arrange
        var (userId, _, card) = await StudentWithACardAsync();
        Clock.Advance(TimeSpan.FromDays(2));
        var reviewedAt = Clock.GetUtcNow();

        // Act
        var result = await ReviewAsync(userId, card.Id, ReviewRating.Good);

        // Assert
        Assert.Equal(reviewedAt.AddDays(1), result.NextReviewAt);
        Assert.Equal(result.NextReviewAt, await StoredNextReviewAsync(card.Id));
        var state = await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{card.Id}'");
        Assert.Contains("\"repetitions\": 1", state);
    }

    [Fact(DisplayName = "As revisões seguem o SM-2: good, good leva de 1 dia a 6 dias")]
    public async Task Review_FollowsTheSm2Intervals()
    {
        // Arrange
        var (userId, _, card) = await StudentWithACardAsync();

        // Act
        var firstAt = Clock.GetUtcNow();
        var first = await ReviewAsync(userId, card.Id, ReviewRating.Good);
        Clock.Advance(TimeSpan.FromDays(1));
        var secondAt = Clock.GetUtcNow();
        var second = await ReviewAsync(userId, card.Id, ReviewRating.Good);

        // Assert
        Assert.Equal(firstAt.AddDays(1), first.NextReviewAt);
        Assert.Equal(secondAt.AddDays(6), second.NextReviewAt);
    }

    [Fact(DisplayName = "Errar de novo (again) reinicia o cartão: de volta a 1 dia")]
    public async Task Review_Again_RestartsTheCard()
    {
        // Arrange
        var (userId, _, card) = await StudentWithACardAsync();
        await ReviewAsync(userId, card.Id, ReviewRating.Good);
        Clock.Advance(TimeSpan.FromDays(1));
        await ReviewAsync(userId, card.Id, ReviewRating.Good);
        Clock.Advance(TimeSpan.FromDays(6));
        var forgotAt = Clock.GetUtcNow();

        // Act
        var result = await ReviewAsync(userId, card.Id, ReviewRating.Again);

        // Assert
        Assert.Equal(forgotAt.AddDays(1), result.NextReviewAt);
    }

    [Fact(DisplayName = "RN-REV-18: revisar não grava tentativa nem mexe no progresso do tópico")]
    public async Task Review_WritesOnlyTheCard()
    {
        // Arrange
        var (userId, _, card) = await StudentWithACardAsync();
        var before = await ScalarAsync<long>("SELECT (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_topic) + (SELECT count(*) FROM student_flashcard)");

        // Act
        await ReviewAsync(userId, card.Id, ReviewRating.Easy);

        // Assert
        Assert.Equal(before, await ScalarAsync<long>("SELECT (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_topic) + (SELECT count(*) FROM student_flashcard)"));
    }

    [Fact(DisplayName = "RN-REV-11: o cartão de outro aluno é review.card.notFound, e nada muda no banco")]
    public async Task Review_TheCardOfAnotherStudent_ThrowsNotFoundAndChangesNothing()
    {
        // Arrange
        var (_, _, card) = await StudentWithACardAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        var stateBefore = await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{card.Id}'");

        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() => ReviewAsync(bia.Id, card.Id, ReviewRating.Good));

        // Assert
        Assert.Equal(ReviewErrors.CardNotFound, exception.Code);
        Assert.Equal(stateBefore, await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{card.Id}'"));
    }

    [Fact(DisplayName = "FA-REV-06: um cartão de algoritmo sem agendador registrado é erro do servidor, e nada é gravado")]
    public async Task Review_WithAnAlgorithmWithoutAScheduler_ThrowsAndChangesNothing()
    {
        // Arrange
        var (userId, _, card) = await StudentWithACardAsync();
        await ExecuteAsync($"UPDATE student_flashcard SET algorithm = 'fsrs' WHERE id = '{card.Id}'");
        var nextBefore = await StoredNextReviewAsync(card.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReviewAsync(userId, card.Id, ReviewRating.Good));
        Assert.Equal(nextBefore, await StoredNextReviewAsync(card.Id));
    }

    [Fact(DisplayName = "FA-REV-07: um estado de agendamento corrompido é erro do servidor, e o cartão não é gravado")]
    public async Task Review_WithACorruptedState_ThrowsAndChangesNothing()
    {
        // Arrange: um JSON válido para o jsonb, mas com valores que o adapter recusa
        var (userId, _, card) = await StudentWithACardAsync();
        await ExecuteAsync($"UPDATE student_flashcard SET scheduling_state = '{{\"repetitions\": -3, \"intervalDays\": 0, \"easeFactor\": 0.5}}' WHERE id = '{card.Id}'");
        var nextBefore = await StoredNextReviewAsync(card.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReviewAsync(userId, card.Id, ReviewRating.Good));
        Assert.Equal(nextBefore, await StoredNextReviewAsync(card.Id));
    }

    [Fact(DisplayName = "PEND-REV-02: duas revisões simultâneas do mesmo cartão com a mesma nota não falham e deixam o mesmo estado")]
    public async Task Review_TwoSimultaneousReviews_LastWriteWinsWithTheSameResult()
    {
        // Arrange: duas requisições carregam o cartão antes de qualquer uma gravar
        var (_, studentId, card) = await StudentWithACardAsync();
        using var first = NewScope();
        using var second = NewScope();
        var firstCard = (await first.StudentFlashcards.FindAsync(card.Id, studentId, Cancel))!;
        var secondCard = (await second.StudentFlashcards.FindAsync(card.Id, studentId, Cancel))!;

        // Act
        firstCard.Review(ReviewRating.Good, Scheduler, Clock);
        secondCard.Review(ReviewRating.Good, Scheduler, Clock);
        await first.UnitOfWork.SaveChangesAsync(Cancel);
        await second.UnitOfWork.SaveChangesAsync(Cancel);

        // Assert
        Assert.Equal(secondCard.NextReviewAt, await StoredNextReviewAsync(card.Id));
        Assert.Equal(firstCard.NextReviewAt, secondCard.NextReviewAt);
    }
}
