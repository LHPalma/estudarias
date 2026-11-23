using Estudarias.Domain.Attempts;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

/// <summary>A tentativa (#84) contra um PostgreSQL real: vai e volta, e as restrições de attempt são recusadas pelo banco.</summary>
[Collection(PostgresCollection.Name)]
public class AttemptRepositoryTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<(SampleContent Content, Student Student)> SeedAsync()
    {
        var content = await SeedContentAsync();
        var student = Samples.Student();
        using var scope = NewScope();
        scope.Students.Add(student);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
        return (content, student);
    }

    private async Task SaveAsync(params Attempt[] attempts)
    {
        using var scope = NewScope();
        foreach (var attempt in attempts)
        {
            scope.Attempts.Add(attempt);
        }

        await scope.UnitOfWork.SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "A tentativa volta do banco com o aluno, o exercício, a alternativa, o tempo e o instante")]
    public async Task Add_RoundTrips()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        var attempt = Attempt.Create(student.Id, content.Exercise.Id, content.Wrong.Id, 48, Clock);
        await SaveAsync(attempt);

        // Act
        var read = await ReadAsync(db => db.Attempts.SingleAsync(value => value.Id == attempt.Id, Cancel));

        // Assert
        Assert.Equal(
            (student.Id, content.Exercise.Id, content.Wrong.Id, (int?)48, Clock.GetUtcNow()),
            (read.StudentId, read.ExerciseId, read.AlternativeId, read.ResponseSeconds, read.AnsweredAt));
    }

    [Fact(DisplayName = "O tempo de resposta é opcional")]
    public async Task Add_WithoutResponseTime_StoresNull()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        var attempt = Attempt.Create(student.Id, content.Exercise.Id, content.Correct.Id, responseSeconds: null, Clock);

        // Act
        await SaveAsync(attempt);

        // Assert
        Assert.Null((await ReadAsync(db => db.Attempts.SingleAsync(value => value.Id == attempt.Id, Cancel))).ResponseSeconds);
    }

    [Fact(DisplayName = "O aluno pode responder o mesmo exercício quantas vezes quiser")]
    public async Task Add_SeveralAttemptsOnTheSameExercise_AreAccepted()
    {
        // Arrange
        var (content, student) = await SeedAsync();

        // Act
        await SaveAsync(
            Attempt.Create(student.Id, content.Exercise.Id, content.Wrong.Id, 30, Clock),
            Attempt.Create(student.Id, content.Exercise.Id, content.Correct.Id, 20, Clock));

        // Assert
        Assert.Equal(2, await ScalarAsync<long>($"SELECT count(*) FROM attempt WHERE student_id = '{student.Id}' AND exercise_id = '{content.Exercise.Id}'"));
    }

    [Fact(DisplayName = "O banco recusa um tempo de resposta negativo")]
    public async Task Attempt_WithANegativeResponseTime_IsRejected()
    {
        // Arrange
        var (content, student) = await SeedAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO attempt (id, student_id, exercise_id, alternative_id, response_seconds, answered_at) VALUES ('{Guid.CreateVersion7()}', '{student.Id}', '{content.Exercise.Id}', '{content.Correct.Id}', -1, now())",
            PostgresErrorCodes.CheckViolation,
            "ck_attempt_response_seconds");
    }

    [Fact(DisplayName = "O banco recusa uma tentativa com uma alternativa que não existe")]
    public async Task Attempt_WithAnUnknownAlternative_IsRejected()
    {
        // Arrange
        var (content, student) = await SeedAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO attempt (id, student_id, exercise_id, alternative_id, answered_at) VALUES ('{Guid.CreateVersion7()}', '{student.Id}', '{content.Exercise.Id}', '{Guid.CreateVersion7()}', now())",
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_attempt_alternative_alternative_id");
    }

    [Fact(DisplayName = "Um exercício já respondido não pode ser apagado, nem as alternativas dele")]
    public async Task Delete_OfAnAnsweredExercise_IsRestricted()
    {
        // Arrange
        var (content, student) = await SeedAsync();
        await SaveAsync(Attempt.Create(student.Id, content.Exercise.Id, content.Wrong.Id, 30, Clock));

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync($"DELETE FROM exercise WHERE id = '{content.Exercise.Id}'"));

        // Assert
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Equal(2, await ScalarAsync<long>($"SELECT count(*) FROM alternative WHERE exercise_id = '{content.Exercise.Id}'"));
    }
}
