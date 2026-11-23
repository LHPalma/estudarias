using System.Text.Json.Nodes;
using Estudarias.Application.Common;
using Estudarias.Domain.Attempts;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

/// <summary>O cartão de revisão do aluno (#84) contra um PostgreSQL real, com o estado do SM-2 em jsonb.</summary>
[Collection(PostgresCollection.Name)]
public class StudentFlashcardRepositoryTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private static readonly Sm2Scheduler Scheduler = new();

    private async Task<(SampleContent Content, Student Student)> SeedAsync()
    {
        var content = await SeedContentAsync();
        var student = Samples.Student();
        using var scope = NewScope();
        scope.Students.Add(student);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
        return (content, student);
    }

    private async Task SaveAsync(StudentFlashcard card, Attempt? origin = null)
    {
        using var scope = NewScope();
        if (origin is not null)
        {
            scope.Attempts.Add(origin);
        }

        scope.StudentFlashcards.Add(card);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
    }

    private async Task<StudentFlashcard?> FindAsync(Guid id, Guid studentId)
    {
        using var scope = NewScope();
        return await scope.StudentFlashcards.FindAsync(id, studentId, Cancel);
    }

    [Fact(DisplayName = "O cartão volta do banco com o algoritmo, o estado, a próxima revisão e a tentativa de origem")]
    public async Task FindAsync_RoundTrips()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        var origin = Attempt.Create(student.Id, content.Exercise.Id, content.Wrong.Id, 48, Clock);
        var card = StudentFlashcard.Create(student.Id, content.Generated.Id, origin.Id, Scheduler, Clock);
        await SaveAsync(card, origin);

        // Act
        var found = await FindAsync(card.Id, student.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(
            (student.Id, content.Generated.Id, (Guid?)origin.Id, "sm2", Clock.GetUtcNow().AddDays(1)),
            (found.StudentId, found.FlashcardId, found.OriginAttemptId, found.Algorithm, found.NextReviewAt));
    }

    [Fact(DisplayName = "O estado do SM-2 volta do jsonb como o mesmo JSON, e o PostgreSQL o lê como JSON")]
    public async Task SchedulingState_RoundTripsAsTheSameJson()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        var card = StudentFlashcard.Create(student.Id, content.Generated.Id, originAttemptId: null, Scheduler, Clock);
        await SaveAsync(card);

        // Act
        var found = await FindAsync(card.Id, student.Id);
        var easeFactor = await ScalarAsync<string>($"SELECT scheduling_state->>'easeFactor' FROM student_flashcard WHERE id = '{card.Id}'");

        // Assert
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(card.SchedulingState), JsonNode.Parse(found!.SchedulingState)));
        Assert.Equal("2.5", easeFactor);
        Assert.Equal(Sm2Scheduler.ReadState(card.SchedulingState), Sm2Scheduler.ReadState(found.SchedulingState));
    }

    [Fact(DisplayName = "Revisar um cartão carregado do banco grava o estado novo e a próxima revisão")]
    public async Task Review_OnALoadedCard_IsSaved()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        var card = StudentFlashcard.Create(student.Id, content.Generated.Id, originAttemptId: null, Scheduler, Clock);
        await SaveAsync(card);
        Clock.Advance(TimeSpan.FromDays(1));

        // Act
        string expectedState;
        DateTimeOffset expectedNext;
        using (var scope = NewScope())
        {
            var loaded = await scope.StudentFlashcards.FindAsync(card.Id, student.Id, Cancel);
            loaded!.Review(ReviewRating.Good, Scheduler, Clock);
            (expectedState, expectedNext) = (loaded.SchedulingState, loaded.NextReviewAt);
            await scope.UnitOfWork.SaveChangesAsync(Cancel);
        }

        // Assert
        var found = await FindAsync(card.Id, student.Id);
        Assert.Equal(expectedNext, found!.NextReviewAt);
        Assert.Equal(Sm2Scheduler.ReadState(expectedState), Sm2Scheduler.ReadState(found.SchedulingState));
        Assert.Equal(1, Sm2Scheduler.ReadState(found.SchedulingState).Repetitions);
    }

    [Fact(DisplayName = "O cartão de outro aluno volta nulo, como um que não existe")]
    public async Task FindAsync_ForAnotherStudent_ReturnsNull()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        var card = StudentFlashcard.Create(student.Id, content.Generated.Id, originAttemptId: null, Scheduler, Clock);
        await SaveAsync(card);

        // Act
        var other = await FindAsync(card.Id, Guid.CreateVersion7());
        var unknown = await FindAsync(Guid.CreateVersion7(), student.Id);

        // Assert
        Assert.Null(other);
        Assert.Null(unknown);
    }

    [Fact(DisplayName = "ExistsAsync diz se o aluno já tem o cartão daquele flashcard")]
    public async Task ExistsAsync_TellsWhetherTheStudentHasTheCard()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        await SaveAsync(StudentFlashcard.Create(student.Id, content.Generated.Id, originAttemptId: null, Scheduler, Clock));
        using var scope = NewScope();

        // Act
        var has = await scope.StudentFlashcards.ExistsAsync(student.Id, content.Generated.Id, Cancel);
        var hasManual = await scope.StudentFlashcards.ExistsAsync(student.Id, content.Manual.Id, Cancel);
        var otherHas = await scope.StudentFlashcards.ExistsAsync(Guid.CreateVersion7(), content.Generated.Id, Cancel);

        // Assert
        Assert.Equal((true, false, false), (has, hasManual, otherHas));
    }

    [Fact(DisplayName = "Um segundo cartão do mesmo aluno para o mesmo flashcard é recusado pela gravação")]
    public async Task Add_DuplicatedCard_ThrowsUniqueConstraintViolation()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        await SaveAsync(StudentFlashcard.Create(student.Id, content.Generated.Id, originAttemptId: null, Scheduler, Clock));

        // Act
        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => SaveAsync(StudentFlashcard.Create(student.Id, content.Generated.Id, originAttemptId: null, Scheduler, Clock)));

        // Assert
        Assert.Equal("ux_student_flashcard_student_id_flashcard_id", exception.ConstraintName);
    }

    [Fact(DisplayName = "O banco recusa um estado de agendamento que não é JSON")]
    public async Task SchedulingState_ThatIsNotJson_IsRejected()
    {
        // Arrange
        var (content, student) = await SeedAsync();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            $"INSERT INTO student_flashcard (id, student_id, flashcard_id, algorithm, scheduling_state, next_review_at) VALUES ('{Guid.CreateVersion7()}', '{student.Id}', '{content.Generated.Id}', 'sm2', 'nao-e-json', now())"));

        // Assert
        Assert.Equal(PostgresErrorCodes.InvalidTextRepresentation, exception.SqlState);
    }

    [Fact(DisplayName = "Um flashcard que algum aluno revisa não pode ser apagado")]
    public async Task Delete_OfAFlashcardWithCards_IsRestricted()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        await SaveAsync(StudentFlashcard.Create(student.Id, content.Manual.Id, originAttemptId: null, Scheduler, Clock));

        // Act & Assert
        await AssertRejectedAsync(
            $"DELETE FROM flashcard WHERE id = '{content.Manual.Id}'",
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_student_flashcard_flashcard_flashcard_id");
    }

    [Fact(DisplayName = "Descartar um cartão marcado para gravar o tira da gravação, e o resto é gravado")]
    public async Task Discard_RemovesTheCardFromThePendingWrite()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        using var scope = NewScope();
        var origin = Attempt.Create(student.Id, content.Exercise.Id, content.Wrong.Id, null, Clock);
        var card = StudentFlashcard.Create(student.Id, content.Generated.Id, origin.Id, Scheduler, Clock);
        scope.Attempts.Add(origin);
        scope.StudentFlashcards.Add(card);

        // Act
        scope.StudentFlashcards.Discard(card);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);

        // Assert
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM attempt"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM student_flashcard"));
    }
}
