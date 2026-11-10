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
}
