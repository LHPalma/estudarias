using Estudarias.Domain.Identity;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class UserRepositoryTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    [Fact(DisplayName = "Uma conta gravada volta inteira numa consulta nova, hash e versão do pepper incluídos")]
    public async Task FindByEmail_AfterSave_ReturnsTheWholeAccount()
    {
        // Arrange
        var student = Samples.Student();
        var user = Samples.User(student, "ana@example.com", pepperVersion: 2);
        using (var scope = NewScope())
        {
            scope.Students.Add(student);
            scope.Users.Add(user);
            await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        using var reading = NewScope();
        var found = await reading.Users.FindByEmailAsync("ana@example.com", CancellationToken.None);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal("ana@example.com", found.Email);
        Assert.Equal(user.PasswordHash.Value, found.PasswordHash.Value);
        Assert.Equal(2, found.PasswordHash.PepperVersion);
        Assert.Equal(UserRole.Student, found.Role);
        Assert.Equal(student.Id, found.StudentId);
        Assert.True(found.IsActive);
    }

    [Fact(DisplayName = "O hash e a versão do pepper ficam em colunas separadas, e o papel como student")]
    public async Task Save_StoresHashAndPepperVersionInSeparateColumns()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        var hash = await ScalarAsync<string>($"SELECT password_hash FROM app_user WHERE id = '{user.Id}'");
        var version = await ScalarAsync<int>($"SELECT password_pepper_version FROM app_user WHERE id = '{user.Id}'");
        var role = await ScalarAsync<string>($"SELECT role FROM app_user WHERE id = '{user.Id}'");

        // Assert
        Assert.Equal(user.PasswordHash.Value, hash);
        Assert.Equal(1, version);
        Assert.Equal("student", role);
    }

    [Fact(DisplayName = "Procurar um e-mail que não existe devolve nulo")]
    public async Task FindByEmail_WithUnknownEmail_ReturnsNull()
    {
        // Arrange
        await SeedUserAsync("ana@example.com");

        // Act
        using var scope = NewScope();
        var found = await scope.Users.FindByEmailAsync("bia@example.com", CancellationToken.None);

        // Assert
        Assert.Null(found);
    }

    [Fact(DisplayName = "A conta é encontrada pelo id")]
    public async Task FindById_ReturnsTheAccount()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        using var scope = NewScope();
        var found = await scope.Users.FindByIdAsync(user.Id, CancellationToken.None);
        var missing = await scope.Users.FindByIdAsync(Guid.CreateVersion7(), CancellationToken.None);

        // Assert
        Assert.Equal(user.Email, found?.Email);
        Assert.Null(missing);
    }

    [Fact(DisplayName = "ExistsByEmail diz se já há uma conta com o e-mail")]
    public async Task ExistsByEmail_ReflectsStoredAccounts()
    {
        // Arrange
        await SeedUserAsync("ana@example.com");

        // Act
        using var scope = NewScope();
        var existing = await scope.Users.ExistsByEmailAsync("ana@example.com", CancellationToken.None);
        var unknown = await scope.Users.ExistsByEmailAsync("bia@example.com", CancellationToken.None);

        // Assert
        Assert.True(existing);
        Assert.False(unknown);
    }

    [Fact(DisplayName = "Duas contas não podem ter o mesmo e-mail")]
    public async Task Save_WithDuplicateEmail_ViolatesTheUniqueIndex()
    {
        // Arrange
        await SeedUserAsync("ana@example.com");
        using var scope = NewScope();
        var otherStudent = Samples.Student();
        scope.Students.Add(otherStudent);
        scope.Users.Add(Samples.User(otherStudent, "ana@example.com"));

        // Act
        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
        Assert.Equal("ux_app_user_email", postgresError.ConstraintName);
    }

    [Fact(DisplayName = "Um aluno tem uma conta só")]
    public async Task Save_WithTwoAccountsForTheSameStudent_ViolatesTheUniqueIndex()
    {
        // Arrange
        var student = Samples.Student();
        using (var seeding = NewScope())
        {
            seeding.Students.Add(student);
            seeding.Users.Add(Samples.User(student, "ana@example.com"));
            await seeding.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        using var scope = NewScope();
        scope.Users.Add(Samples.User(student, "outra@example.com"));

        // Act
        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("ux_app_user_student_id", postgresError.ConstraintName);
    }

    [Fact(DisplayName = "Uma conta não pode apontar para um aluno que não existe")]
    public async Task Save_WithUnknownStudent_ViolatesTheForeignKey()
    {
        // Arrange
        using var scope = NewScope();
        scope.Users.Add(Samples.User(Samples.Student()));

        // Act
        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresError.SqlState);
    }

    [Fact(DisplayName = "O cadastro grava o aluno e a conta juntos: se a conta falha, o aluno também não entra")]
    public async Task Save_WhenTheAccountFails_DoesNotKeepTheStudent()
    {
        // Arrange
        await SeedUserAsync("ana@example.com");
        var newStudent = Samples.Student();
        using (var scope = NewScope())
        {
            scope.Students.Add(newStudent);
            scope.Users.Add(Samples.User(newStudent, "ana@example.com"));

            // Act
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));
        }

        // Assert
        var students = await ScalarAsync<long>("SELECT count(*) FROM student");
        Assert.Equal(1, students);
    }
}
