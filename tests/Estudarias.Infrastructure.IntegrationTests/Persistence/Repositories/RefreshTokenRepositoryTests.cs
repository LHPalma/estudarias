using Estudarias.Application.Common;
using Estudarias.Domain.Identity;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class RefreshTokenRepositoryTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task SaveAsync(params RefreshToken[] tokens)
    {
        using var scope = NewScope();
        foreach (var token in tokens)
        {
            scope.RefreshTokens.Add(token);
        }

        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    [Fact(DisplayName = "Um token gravado é encontrado pelo hash, com todos os campos")]
    public async Task FindByHash_AfterSave_ReturnsTheToken()
    {
        // Arrange
        var user = await SeedUserAsync();
        var familyId = Guid.CreateVersion7();
        var token = Samples.Token(user.Id, familyId, 'a');
        await SaveAsync(token);

        // Act
        using var scope = NewScope();
        var found = await scope.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(token.Id, found.Id);
        Assert.Equal(user.Id, found.UserId);
        Assert.Equal(familyId, found.FamilyId);
        Assert.Equal(Samples.Now + Samples.RefreshLifetime, found.ExpiresAt);
        Assert.Null(found.RevokedAt);
        Assert.Null(found.ReplacedByTokenId);
    }

    [Fact(DisplayName = "Procurar um hash que não existe devolve nulo")]
    public async Task FindByHash_WithUnknownHash_ReturnsNull()
    {
        // Act
        using var scope = NewScope();
        var found = await scope.RefreshTokens.FindByHashAsync(Samples.TokenHash('z'), CancellationToken.None);

        // Assert
        Assert.Null(found);
    }

    [Fact(DisplayName = "Dois tokens não podem ter o mesmo hash")]
    public async Task Save_WithDuplicateHash_ViolatesTheUniqueIndex()
    {
        // Arrange
        var user = await SeedUserAsync();
        await SaveAsync(Samples.Token(user.Id, Guid.CreateVersion7(), 'a'));
        using var scope = NewScope();
        scope.RefreshTokens.Add(Samples.Token(user.Id, Guid.CreateVersion7(), 'a'));

        // Act
        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.Equal("ux_refresh_token_token_hash", exception.ConstraintName);
    }

    [Fact(DisplayName = "A revogação e o sucessor ficam gravados")]
    public async Task Revoke_IsPersisted()
    {
        // Arrange
        var user = await SeedUserAsync();
        await SaveAsync(Samples.Token(user.Id, Guid.CreateVersion7(), 'a'));
        var successorId = Guid.CreateVersion7();
        var revokedAt = Samples.Now.AddMinutes(5);

        using (var scope = NewScope())
        {
            var token = await scope.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);
            token!.Revoke(revokedAt, successorId);
            await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        using var reading = NewScope();
        var found = await reading.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None);

        // Assert
        Assert.Equal(revokedAt, found!.RevokedAt);
        Assert.Equal(successorId, found.ReplacedByTokenId);
    }

    [Fact(DisplayName = "Listar a família traz só os tokens não revogados dela")]
    public async Task ListUnrevokedByFamily_ReturnsOnlyUnrevokedTokensOfTheFamily()
    {
        // Arrange
        var user = await SeedUserAsync();
        var family = Guid.CreateVersion7();
        var revoked = Samples.Token(user.Id, family, 'a');
        revoked.Revoke(Samples.Now);
        var active = Samples.Token(user.Id, family, 'b');
        var otherFamily = Samples.Token(user.Id, Guid.CreateVersion7(), 'c');
        await SaveAsync(revoked, active, otherFamily);

        // Act
        using var scope = NewScope();
        var tokens = await scope.RefreshTokens.ListUnrevokedByFamilyAsync(family, CancellationToken.None);

        // Assert
        var only = Assert.Single(tokens);
        Assert.Equal(active.Id, only.Id);
    }

    [Fact(DisplayName = "Remover os vencidos tira só os do usuário que já expiraram, e só depois de gravar")]
    public async Task RemoveExpired_RemovesOnlyTheUsersExpiredTokens()
    {
        // Arrange
        var ana = await SeedUserAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        var now = Samples.Now;
        var anaExpired = Samples.Token(ana.Id, Guid.CreateVersion7(), 'a', issuedAt: now - Samples.RefreshLifetime - TimeSpan.FromDays(1));
        var anaActive = Samples.Token(ana.Id, Guid.CreateVersion7(), 'b', issuedAt: now);
        var biaExpired = Samples.Token(bia.Id, Guid.CreateVersion7(), 'c', issuedAt: now - Samples.RefreshLifetime - TimeSpan.FromDays(1));
        await SaveAsync(anaExpired, anaActive, biaExpired);

        // Act
        using (var scope = NewScope())
        {
            await scope.RefreshTokens.RemoveExpiredAsync(ana.Id, now, CancellationToken.None);
            var beforeSave = await ScalarAsync<long>("SELECT count(*) FROM refresh_token");
            await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, beforeSave);
        }

        using var reading = NewScope();
        Assert.Null(await reading.RefreshTokens.FindByHashAsync(Samples.TokenHash('a'), CancellationToken.None));
        Assert.NotNull(await reading.RefreshTokens.FindByHashAsync(Samples.TokenHash('b'), CancellationToken.None));
        Assert.NotNull(await reading.RefreshTokens.FindByHashAsync(Samples.TokenHash('c'), CancellationToken.None));
    }

    [Fact(DisplayName = "Um token não pode apontar para um usuário que não existe")]
    public async Task Save_WithUnknownUser_ViolatesTheForeignKey()
    {
        // Arrange
        using var scope = NewScope();
        scope.RefreshTokens.Add(Samples.Token(Guid.CreateVersion7(), Guid.CreateVersion7(), 'a'));

        // Act
        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None));

        // Assert
        var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresError.SqlState);
    }
}
