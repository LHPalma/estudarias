using Estudarias.Application.Trail;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Trail;

/// <summary>A liberação inicial pelo composition root e contra o PostgreSQL: o que fica gravado em student_topic.</summary>
[Collection(PostgresCollection.Name)]
public class UnlockInitialTopicsFlowTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task UnlockAsync(Guid userId)
    {
        using var scope = NewScope();
        await scope.Get<UnlockInitialTopicsHandler>().Handle(userId, Cancel);
    }

    private async Task<Student> FindStudentAsync(Guid studentId)
    {
        using var scope = NewScope();
        return (await scope.Students.FindAsync(studentId, Cancel))!;
    }

    [Fact(DisplayName = "O aluno novo fica com a raiz disponível e os dependentes bloqueados, gravados no banco")]
    public async Task Unlock_ForANewStudent_StoresOnlyTheRoot()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        await UnlockAsync(user.Id);

        // Assert
        var student = await FindStudentAsync(user.StudentId!.Value);
        Assert.Equal(TopicStatus.Available, student.StatusOf(content.Basics.Id));
        Assert.Equal(TopicStatus.Locked, student.StatusOf(content.Fractions.Id));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM student_topic"));
    }

    [Fact(DisplayName = "Abrir a trilha de novo não duplica nem desfaz o progresso")]
    public async Task Unlock_Twice_IsIdempotent()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await UnlockAsync(user.Id);
        await ExecuteAsync($"UPDATE student_topic SET mastered_at = unlocked_at WHERE topic_id = '{content.Basics.Id}'");

        // Act
        await UnlockAsync(user.Id);

        // Assert
        var student = await FindStudentAsync(user.StudentId!.Value);
        Assert.Equal(TopicStatus.Mastered, student.StatusOf(content.Basics.Id));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM student_topic"));
    }

    [Fact(DisplayName = "Sem conteúdo carregado, nada é gravado")]
    public async Task Unlock_WithoutContent_WritesNothing()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        await UnlockAsync(user.Id);

        // Assert
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM student_topic"));
    }
}
