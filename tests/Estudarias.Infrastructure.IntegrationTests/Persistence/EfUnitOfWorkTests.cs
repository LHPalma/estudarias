using Estudarias.Application.Common;
using Estudarias.Domain.Identity;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public class EfUnitOfWorkTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    // O provider InMemory ignora o token de concorrência: só um PostgreSQL de verdade prova esta regra.
    [Fact(DisplayName = "De duas revogações simultâneas do mesmo token, só a primeira vence")]
    public async Task SaveChanges_WithTwoConcurrentRevocations_FailsTheSecond()
    {
        // Arrange
        var user = await SeedUserAsync();
        using (var seeding = NewScope())
        {
            seeding.RefreshTokens.Add(Samples.Token(user.Id, Guid.CreateVersion7(), 'a'));
            await seeding.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        using var first = NewScope();
        using var second = NewScope();
        var firstCopy = await first.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);
        var secondCopy = await second.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);
        var firstRevocation = Samples.Now.AddMinutes(1);

        firstCopy!.Revoke(firstRevocation);
        secondCopy!.Revoke(Samples.Now.AddMinutes(2));

        // Act
        await first.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => second.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.NotNull(exception.InnerException);
        using var reading = NewScope();
        var stored = await reading.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);
        Assert.Equal(firstRevocation, stored!.RevokedAt);
    }

    [Fact(DisplayName = "A revogação de um token já revogado por outra requisição falha como conflito, não como erro de banco")]
    public async Task SaveChanges_WhenTheTokenWasRevokedMeanwhile_ThrowsConcurrencyConflict()
    {
        // Arrange
        var user = await SeedUserAsync();
        using (var seeding = NewScope())
        {
            seeding.RefreshTokens.Add(Samples.Token(user.Id, Guid.CreateVersion7(), 'a'));
            await seeding.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        using var scope = NewScope();
        var token = await scope.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);
        token!.Revoke(Samples.Now.AddMinutes(2));
        await ExecuteAsync($"UPDATE refresh_token SET revoked_at = now() WHERE id = '{token.Id}'");

        // Act & Assert
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));
    }

    [Fact(DisplayName = "Dois cadastros simultâneos com o mesmo e-mail: o que perde a corrida recebe a violação de unicidade da Application")]
    public async Task SaveChanges_WithTwoConcurrentRegistrationsOfTheSameEmail_FailsTheLoserAsUniqueViolation()
    {
        // Arrange: as duas requisições já checaram que o e-mail estava livre, então só o índice único decide.
        using var first = NewScope();
        using var second = NewScope();
        var firstStudent = Samples.Student();
        var secondStudent = Samples.Student();
        first.Students.Add(firstStudent);
        first.Users.Add(Samples.User(firstStudent, "ana@example.com"));
        second.Students.Add(secondStudent);
        second.Users.Add(Samples.User(secondStudent, "ana@example.com"));

        // Act
        await first.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => second.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.Equal("ux_app_user_email", exception.ConstraintName);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM app_user"));
    }

    [Fact(DisplayName = "Uma violação que não é de unicidade segue como erro do banco, sem virar conflito")]
    public async Task SaveChanges_WithAForeignKeyViolation_IsNotTranslated()
    {
        // Arrange
        using var scope = NewScope();
        scope.Users.Add(Samples.User(Samples.Student()));

        // Act & Assert
        await Assert.ThrowsAnyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));
    }

    [Fact(DisplayName = "Gravar sem conflito não lança")]
    public async Task SaveChanges_WithoutConflict_Succeeds()
    {
        // Act
        var user = await SeedUserAsync();

        // Assert
        using var scope = NewScope();
        Assert.NotNull(await scope.Users.FindByIdAsync(user.Id, CancellationToken.None));
        Assert.Equal(UserRole.Student, user.Role);
    }
}
