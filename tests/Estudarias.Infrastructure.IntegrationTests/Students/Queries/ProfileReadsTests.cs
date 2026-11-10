using Estudarias.Application.Students.Queries;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Estudarias.Infrastructure.IntegrationTests.Students.Queries;

[Collection(PostgresCollection.Name)]
public class ProfileReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<ProfileResponse?> FindAsync(Guid userId)
    {
        using var scope = NewScope();
        return await scope.Get<IProfileReads>().FindAsync(userId, CancellationToken.None);
    }

    [Fact(DisplayName = "O perfil traz a conta e o aluno ligado a ela")]
    public async Task FindAsync_ForAnActiveAccount_ReturnsTheAccountAndTheStudent()
    {
        // Arrange
        var user = await SeedUserAsync("ana@example.com");

        // Act
        var profile = await FindAsync(user.Id);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.User.Id);
        Assert.Equal("ana@example.com", profile.User.Email);
        Assert.Equal(UserRole.Student, profile.User.Role);
        Assert.Equal(user.StudentId, profile.Student.Id);
        Assert.Equal("Ana", profile.Student.Name);
    }

    [Theory(DisplayName = "O grupo experimental do perfil é o que está gravado no aluno")]
    [InlineData(ExperimentalGroup.Control)]
    [InlineData(ExperimentalGroup.Diagnostic)]
    public async Task FindAsync_ReturnsTheStoredExperimentalGroup(ExperimentalGroup group)
    {
        // Arrange
        var user = await SeedUserAsync(group: group);

        // Act
        var profile = await FindAsync(user.Id);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(group, profile.Student.ExperimentalGroup);
    }

    [Fact(DisplayName = "Cada conta recebe o perfil do próprio aluno, e não o de outra")]
    public async Task FindAsync_WithTwoAccounts_ReturnsEachOwnStudent()
    {
        // Arrange
        var ana = await SeedUserAsync("ana@example.com", ExperimentalGroup.Control);
        var bia = await SeedUserAsync("bia@example.com", ExperimentalGroup.Diagnostic);

        // Act
        var anaProfile = await FindAsync(ana.Id);
        var biaProfile = await FindAsync(bia.Id);

        // Assert
        Assert.Equal((ana.Email, ana.StudentId, ExperimentalGroup.Control),
            (anaProfile!.User.Email, anaProfile.Student.Id, anaProfile.Student.ExperimentalGroup));
        Assert.Equal((bia.Email, bia.StudentId, ExperimentalGroup.Diagnostic),
            (biaProfile!.User.Email, biaProfile.Student.Id, biaProfile.Student.ExperimentalGroup));
    }

    [Fact(DisplayName = "Uma conta que não existe não tem perfil")]
    public async Task FindAsync_ForAnUnknownAccount_ReturnsNull()
    {
        // Arrange
        await SeedUserAsync();

        // Act
        var profile = await FindAsync(Guid.CreateVersion7());

        // Assert
        Assert.Null(profile);
    }

    [Fact(DisplayName = "Uma conta desabilitada não tem perfil")]
    public async Task FindAsync_ForADisabledAccount_ReturnsNull()
    {
        // Arrange
        var user = await SeedUserAsync();
        await ExecuteAsync($"UPDATE app_user SET is_active = false WHERE id = '{user.Id}'");

        // Act
        var profile = await FindAsync(user.Id);

        // Assert
        Assert.Null(profile);
    }

    [Fact(DisplayName = "Uma conta sem aluno ligado não tem perfil de estudo")]
    public async Task FindAsync_ForAnAccountWithoutAStudent_ReturnsNull()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO app_user (id, email, role, student_id, is_active, password_pepper_version, password_hash) " +
            $"VALUES ('{userId}', 'sem-aluno@example.com', 'student', NULL, true, 1, '$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA')");

        // Act
        var profile = await FindAsync(userId);

        // Assert
        Assert.Null(profile);
    }

    [Fact(DisplayName = "A consulta lê só as colunas do contrato: o hash da senha nunca sai do banco")]
    public async Task FindAsync_NeverSelectsThePasswordHash()
    {
        // Arrange
        var user = await SeedUserAsync();
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options;
        await using var context = new AppDbContext(options);

        // Act
        var profile = await new ProfileReads(context).FindAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(profile);
        var sql = string.Join('\n', commands);
        Assert.Contains("email", sql);
        Assert.DoesNotContain("password_hash", sql);
        Assert.DoesNotContain("password_pepper_version", sql);
    }

    [Fact(DisplayName = "A leitura é uma consulta só, que junta a conta e o aluno")]
    public async Task FindAsync_RunsASingleQuery()
    {
        // Arrange
        var user = await SeedUserAsync();
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options;
        await using var context = new AppDbContext(options);

        // Act
        await new ProfileReads(context).FindAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.Single(commands);
    }
}
