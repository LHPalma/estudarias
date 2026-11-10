using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.UnitTests.Support;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Estudarias.Application.UnitTests.Identity;

public class RefreshSessionHandlerTests
{
    private const string Raw = "raw-refresh-token";
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAccessTokenIssuer _accessTokens = Substitute.For<IAccessTokenIssuer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start.AddMinutes(5));
    private readonly List<RefreshToken> _addedTokens = [];

    private readonly User _user = TestUsers.Active();
    private readonly Guid _family = Guid.CreateVersion7();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public RefreshSessionHandlerTests()
    {
        _accessTokens.Issue(Arg.Any<User>()).Returns(new IssuedAccessToken("access-token", Start.AddMinutes(20)));
        _refreshTokens.When(repository => repository.Add(Arg.Any<RefreshToken>())).Do(call => _addedTokens.Add(call.Arg<RefreshToken>()));
        _users.FindByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private RefreshSessionHandler Handler() => new(_users, _refreshTokens, _accessTokens, _unitOfWork, _clock);

    private RefreshToken GivenAStoredToken(DateTimeOffset? issuedAt = null)
    {
        var token = RefreshToken.Issue(_user.Id, _family, RefreshTokenSecret.Hash(Raw), issuedAt ?? Start, Lifetime);
        _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(Raw), Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    private async Task AssertInvalidRefreshToken(string raw = Raw)
    {
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(
            () => Handler().Handle(new RefreshSessionCommand(raw), Cancel));
        Assert.Equal("identity.refreshToken.invalid", exception.Code);
        _accessTokens.DidNotReceive().Issue(Arg.Any<User>());
    }

    [Fact(DisplayName = "Renovar devolve um par novo: access novo, refresh diferente do usado e a conta")]
    public async Task Handle_WithAnActiveToken_ReturnsANewPair()
    {
        // Arrange
        GivenAStoredToken();

        // Act
        var payload = await Handler().Handle(new RefreshSessionCommand(Raw), Cancel);

        // Assert
        Assert.Equal("access-token", payload.AccessToken);
        Assert.Equal(Start.AddMinutes(20), payload.AccessTokenExpiresAt);
        Assert.NotEqual(Raw, payload.RefreshToken);
        Assert.Matches("^[0-9a-f]{64}$", payload.RefreshToken);
        Assert.Same(_user, payload.User);
    }

    [Fact(DisplayName = "O token usado é revogado e aponta o sucessor, que nasce ativo na mesma família")]
    public async Task Handle_RotatesTheTokenWithinTheSameFamily()
    {
        // Arrange
        var existing = GivenAStoredToken();

        // Act
        var payload = await Handler().Handle(new RefreshSessionCommand(Raw), Cancel);

        // Assert
        var successor = Assert.Single(_addedTokens);
        Assert.Equal(_clock.GetUtcNow(), existing.RevokedAt);
        Assert.Equal(successor.Id, existing.ReplacedByTokenId);
        Assert.Equal(_family, successor.FamilyId);
        Assert.Equal(_user.Id, successor.UserId);
        Assert.Equal(RefreshTokenSecret.Hash(payload.RefreshToken), successor.TokenHash);
        Assert.Equal(_clock.GetUtcNow() + Lifetime, successor.ExpiresAt);
        Assert.True(successor.IsActive(_clock.GetUtcNow()));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token desconhecido é recusado sem gravar nada")]
    public async Task Handle_WithUnknownToken_ThrowsInvalidRefreshToken()
    {
        // Act & Assert
        await AssertInvalidRefreshToken("nunca-emitido");
        Assert.Empty(_addedTokens);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token nulo é recusado como desconhecido, sem estourar")]
    public async Task Handle_WithNullToken_ThrowsInvalidRefreshToken()
    {
        // Act & Assert
        await AssertInvalidRefreshToken(null!);
    }

    [Fact(DisplayName = "Um token vencido é recusado sem ser revogado nem gerar sucessor")]
    public async Task Handle_WithExpiredToken_ThrowsInvalidRefreshToken()
    {
        // Arrange
        var existing = GivenAStoredToken(issuedAt: Start.AddDays(-31));

        // Act & Assert
        await AssertInvalidRefreshToken();
        Assert.Null(existing.RevokedAt);
        Assert.Empty(_addedTokens);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token já usado que reaparece revoga a família inteira e é recusado")]
    public async Task Handle_WithAnAlreadyUsedToken_RevokesTheWholeFamily()
    {
        // Arrange
        var used = GivenAStoredToken();
        used.Revoke(Start.AddMinutes(1));
        var siblingA = RefreshToken.Issue(_user.Id, _family, "a", Start, Lifetime);
        var siblingB = RefreshToken.Issue(_user.Id, _family, "b", Start, Lifetime);
        _refreshTokens.ListUnrevokedByFamilyAsync(_family, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { siblingA, siblingB });

        // Act & Assert
        await AssertInvalidRefreshToken();
        Assert.Equal(_clock.GetUtcNow(), siblingA.RevokedAt);
        Assert.Equal(_clock.GetUtcNow(), siblingB.RevokedAt);
        Assert.Empty(_addedTokens);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _users.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Se a família já estava toda revogada, reaparecer um token usado é recusado sem gravar")]
    public async Task Handle_WithAnAlreadyUsedTokenAndNothingLeftToRevoke_DoesNotSave()
    {
        // Arrange
        GivenAStoredToken().Revoke(Start.AddMinutes(1));
        _refreshTokens.ListUnrevokedByFamilyAsync(_family, Arg.Any<CancellationToken>()).Returns(new List<RefreshToken>());

        // Act & Assert
        await AssertInvalidRefreshToken();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Se outra requisição revogou parte da família nesse meio-tempo, o resultado é o mesmo")]
    public async Task Handle_WhenRevokingTheFamilyConflicts_StillThrowsInvalidRefreshToken()
    {
        // Arrange
        GivenAStoredToken().Revoke(Start.AddMinutes(1));
        _refreshTokens.ListUnrevokedByFamilyAsync(_family, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { RefreshToken.Issue(_user.Id, _family, "a", Start, Lifetime) });
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException(new InvalidOperationException()));

        // Act & Assert
        await AssertInvalidRefreshToken();
    }

    [Fact(DisplayName = "Um usuário desabilitado não renova a sessão, e o token não é revogado")]
    public async Task Handle_WithInactiveUser_ThrowsInvalidRefreshToken()
    {
        // Arrange
        var inactive = TestUsers.Inactive();
        var token = RefreshToken.Issue(inactive.Id, _family, RefreshTokenSecret.Hash(Raw), Start, Lifetime);
        _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(Raw), Arg.Any<CancellationToken>()).Returns(token);
        _users.FindByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);

        // Act & Assert
        await AssertInvalidRefreshToken();
        Assert.Null(token.RevokedAt);
        Assert.Empty(_addedTokens);
    }

    [Fact(DisplayName = "Um token cuja conta não existe mais é recusado")]
    public async Task Handle_WhenTheUserNoLongerExists_ThrowsInvalidRefreshToken()
    {
        // Arrange
        var orphan = RefreshToken.Issue(Guid.CreateVersion7(), _family, RefreshTokenSecret.Hash(Raw), Start, Lifetime);
        _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(Raw), Arg.Any<CancellationToken>()).Returns(orphan);

        // Act & Assert
        await AssertInvalidRefreshToken();
    }

    [Fact(DisplayName = "De duas renovações simultâneas do mesmo token, a que perde a corrida recebe token inválido e nenhum access")]
    public async Task Handle_WhenTheRotationLosesTheRace_ThrowsInvalidRefreshToken()
    {
        // Arrange
        GivenAStoredToken();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException(new InvalidOperationException()));

        // Act & Assert
        await AssertInvalidRefreshToken();
    }
}
