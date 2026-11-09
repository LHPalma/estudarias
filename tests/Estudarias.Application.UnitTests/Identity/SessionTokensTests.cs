using Estudarias.Application.Identity;

namespace Estudarias.Application.UnitTests.Identity;

public class SessionTokensTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "O refresh token novo vale 30 dias, na família e para o usuário informados")]
    public void IssueRefreshToken_ExpiresInThirtyDaysInTheGivenFamily()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();

        // Act
        var (token, _) = SessionTokens.IssueRefreshToken(userId, familyId, Now);

        // Assert
        Assert.Equal(userId, token.UserId);
        Assert.Equal(familyId, token.FamilyId);
        Assert.Equal(Now.AddDays(30), token.ExpiresAt);
        Assert.True(token.IsActive(Now));
    }

    [Fact(DisplayName = "A entidade guarda só o hash: o valor cru devolvido confere com ele")]
    public void IssueRefreshToken_StoresOnlyTheHashOfTheRawValue()
    {
        // Act
        var (token, raw) = SessionTokens.IssueRefreshToken(Guid.CreateVersion7(), Guid.CreateVersion7(), Now);

        // Assert
        Assert.NotEqual(raw, token.TokenHash);
        Assert.Equal(RefreshTokenSecret.Hash(raw), token.TokenHash);
    }
}
