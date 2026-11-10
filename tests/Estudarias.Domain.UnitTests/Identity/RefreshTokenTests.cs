using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;

namespace Estudarias.Domain.UnitTests.Identity;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static RefreshToken NewToken() =>
        RefreshToken.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Start, Lifetime);

    [Fact(DisplayName = "Um token nasce ativo e expira depois da vida útil informada")]
    public void Issue_CreatesActiveTokenThatExpiresAfterLifetime()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();

        // Act
        var token = RefreshToken.Issue(userId, familyId, "hash", Start, Lifetime);

        // Assert
        Assert.Equal(userId, token.UserId);
        Assert.Equal(familyId, token.FamilyId);
        Assert.Equal("hash", token.TokenHash);
        Assert.Equal(Start + Lifetime, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
        Assert.True(token.IsActive(Start));
    }

    [Fact(DisplayName = "Um token vencido não está ativo")]
    public void IsActive_AtExpiration_ReturnsFalse()
    {
        // Arrange
        var token = NewToken();

        // Act & Assert
        Assert.True(token.IsActive(Start + Lifetime - TimeSpan.FromSeconds(1)));
        Assert.False(token.IsActive(Start + Lifetime));
    }

    [Fact(DisplayName = "Revogar registra o instante e o sucessor, e o token deixa de estar ativo")]
    public void Revoke_RecordsInstantAndSuccessor()
    {
        // Arrange
        var token = NewToken();
        var successorId = Guid.CreateVersion7();
        var now = Start.AddMinutes(5);

        // Act
        token.Revoke(now, successorId);

        // Assert
        Assert.Equal(now, token.RevokedAt);
        Assert.Equal(successorId, token.ReplacedByTokenId);
        Assert.False(token.IsActive(now));
    }

    [Fact(DisplayName = "Um token revogado sozinho não tem sucessor")]
    public void Revoke_WithoutSuccessor_LeavesReplacedByEmpty()
    {
        // Arrange
        var token = NewToken();

        // Act
        token.Revoke(Start);

        // Assert
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact(DisplayName = "Um token só pode ser revogado uma vez")]
    public void Revoke_Twice_ThrowsDomainConflictException()
    {
        // Arrange
        var token = NewToken();
        token.Revoke(Start);

        // Act & Assert
        var exception = Assert.Throws<DomainConflictException>(() => token.Revoke(Start.AddMinutes(1)));
        Assert.Equal("identity.refreshToken.alreadyRevoked", exception.Code);
    }
}
