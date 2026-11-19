using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class StudentRepositoryTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    [Theory(DisplayName = "O grupo experimental é gravado em minúsculas, com o nome do enum")]
    [InlineData(ExperimentalGroup.Control, "control")]
    [InlineData(ExperimentalGroup.Diagnostic, "diagnostic")]
    public async Task Save_StoresTheGroupInLowercase(ExperimentalGroup group, string stored)
    {
        // Arrange
        var student = Samples.Student(group);
        using (var scope = NewScope())
        {
            scope.Students.Add(student);
            await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        var column = await ScalarAsync<string>($"SELECT experimental_group FROM student WHERE id = '{student.Id}'");
        var name = await ScalarAsync<string>($"SELECT name FROM student WHERE id = '{student.Id}'");

        // Assert
        Assert.Equal(stored, column);
        Assert.Equal("Ana", name);
    }

    [Fact(DisplayName = "O banco recusa um grupo experimental que não seja control nem diagnostic")]
    public async Task Insert_WithUnknownGroup_ViolatesTheCheckConstraint()
    {
        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            $"INSERT INTO student (id, name, experimental_group) VALUES ('{Guid.CreateVersion7()}', 'Ana', 'outro')"));

        // Assert
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_student_experimental_group", exception.ConstraintName);
    }

    // ---------- Progresso por tópico (student_topic, #84) ----------

    private async Task<Student> SaveAsync(Student student)
    {
        using var scope = NewScope();
        scope.Students.Add(student);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
        return student;
    }

    private async Task<Student?> FindAsync(Guid id)
    {
        using var scope = NewScope();
        return await scope.Students.FindAsync(id, Cancel);
    }

    [Fact(DisplayName = "O progresso volta com o aluno: o desbloqueio e o domínio, com as datas")]
    public async Task FindAsync_ReturnsTheStudentWithItsProgress()
    {
        // Arrange
        var content = await SeedContentAsync();
        var student = Samples.Student();
        student.Unlock(content.Basics.Id, Clock);
        var unlockedAt = Clock.GetUtcNow();
        Clock.Advance(TimeSpan.FromMinutes(5));
        student.MarkMastered(content.Basics.Id, Clock);
        student.Unlock(content.Fractions.Id, Clock);
        await SaveAsync(student);

        // Act
        var found = await FindAsync(student.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(2, found.Progress.Count);
        Assert.Equal(TopicStatus.Mastered, found.StatusOf(content.Basics.Id));
        Assert.Equal(TopicStatus.Available, found.StatusOf(content.Fractions.Id));

        var basics = found.Progress.Single(progress => progress.TopicId == content.Basics.Id);
        Assert.Equal((unlockedAt, (DateTimeOffset?)unlockedAt.AddMinutes(5)), (basics.UnlockedAt, basics.MasteredAt));
        Assert.Null(found.Progress.Single(progress => progress.TopicId == content.Fractions.Id).MasteredAt);
    }

    [Fact(DisplayName = "Um tópico sem linha de progresso continua bloqueado")]
    public async Task FindAsync_WithoutProgress_LeavesEveryTopicLocked()
    {
        // Arrange
        var content = await SeedContentAsync();
        var student = await SaveAsync(Samples.Student());

        // Act
        var found = await FindAsync(student.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Empty(found.Progress);
        Assert.Equal(TopicStatus.Locked, found.StatusOf(content.Basics.Id));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM student_topic"));
    }

    [Fact(DisplayName = "Desbloquear e dominar num aluno carregado do banco é gravado pela unidade de trabalho")]
    public async Task Unlock_OnALoadedStudent_IsSaved()
    {
        // Arrange
        var content = await SeedContentAsync();
        var student = await SaveAsync(Samples.Student());

        // Act
        using (var scope = NewScope())
        {
            var loaded = await scope.Students.FindAsync(student.Id, Cancel);
            loaded!.Unlock(content.Basics.Id, Clock);
            loaded.Unlock(content.Fractions.Id, Clock);
            await scope.UnitOfWork.SaveChangesAsync(Cancel);
        }

        using (var scope = NewScope())
        {
            var loaded = await scope.Students.FindAsync(student.Id, Cancel);
            loaded!.MarkMastered(content.Basics.Id, Clock);
            await scope.UnitOfWork.SaveChangesAsync(Cancel);
        }

        // Assert
        var found = await FindAsync(student.Id);
        Assert.Equal(TopicStatus.Mastered, found!.StatusOf(content.Basics.Id));
        Assert.Equal(TopicStatus.Available, found.StatusOf(content.Fractions.Id));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM student_topic"));
    }

    [Fact(DisplayName = "Um aluno que não existe volta nulo")]
    public async Task FindAsync_ForAnUnknownStudent_ReturnsNull()
    {
        // Act
        var found = await FindAsync(Guid.CreateVersion7());

        // Assert
        Assert.Null(found);
    }

    [Fact(DisplayName = "O banco recusa um segundo progresso do mesmo aluno no mesmo tópico")]
    public async Task StudentTopic_Duplicated_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();
        var student = Samples.Student();
        student.Unlock(content.Basics.Id, Clock);
        await SaveAsync(student);

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO student_topic (id, student_id, topic_id, unlocked_at) VALUES ('{Guid.CreateVersion7()}', '{student.Id}', '{content.Basics.Id}', now())",
            PostgresErrorCodes.UniqueViolation,
            "ux_student_topic_student_id_topic_id");
    }

    [Fact(DisplayName = "O banco recusa um domínio anterior ao desbloqueio")]
    public async Task StudentTopic_MasteredBeforeUnlocked_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();
        var student = await SaveAsync(Samples.Student());

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO student_topic (id, student_id, topic_id, unlocked_at, mastered_at) VALUES ('{Guid.CreateVersion7()}', '{student.Id}', '{content.Basics.Id}', now(), now() - interval '1 minute')",
            PostgresErrorCodes.CheckViolation,
            "ck_student_topic_mastered_after_unlocked");
    }

    [Fact(DisplayName = "O banco recusa progresso num tópico que não existe")]
    public async Task StudentTopic_ForAnUnknownTopic_IsRejected()
    {
        // Arrange
        var student = await SaveAsync(Samples.Student());

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO student_topic (id, student_id, topic_id, unlocked_at) VALUES ('{Guid.CreateVersion7()}', '{student.Id}', '{Guid.CreateVersion7()}', now())",
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_student_topic_topic_topic_id");
    }

    [Fact(DisplayName = "Um tópico liberado para algum aluno não pode ser apagado")]
    public async Task Delete_OfATopicWithProgress_IsRestricted()
    {
        // Arrange
        var content = await SeedContentAsync();
        var student = Samples.Student();
        student.Unlock(content.Basics.Id, Clock);
        await SaveAsync(student);
        await ExecuteAsync($"DELETE FROM topic_prerequisite WHERE prerequisite_id = '{content.Basics.Id}'");

        // Act & Assert
        await AssertRejectedAsync(
            $"DELETE FROM topic WHERE id = '{content.Basics.Id}'",
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_student_topic_topic_topic_id");
    }
}
