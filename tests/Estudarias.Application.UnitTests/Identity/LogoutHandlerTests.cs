using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Estudarias.Application.UnitTests.Identity;

public class LogoutHandlerTests
{
    private const string Raw = "raw-refresh-token";
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start.AddMinutes(5));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private LogoutHandler Handler() => new(_refreshTokens, _unitOfWork, _clock);

    private RefreshToken GivenAStoredToken(DateTimeOffset? issuedAt = null)
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), RefreshTokenSecret.Hash(Raw), issuedAt ?? Start, Lifetime);
        _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(Raw), Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    [Fact(DisplayName = "O logout revoga o refresh token daquele dispositivo e grava")]
    public async Task Handle_WithAnActiveToken_RevokesItAndSaves()
    {
        // Arrange
        var token = GivenAStoredToken();

        // Act
        await Handler().Handle(new LogoutCommand(Raw), Cancel);

        // Assert
        Assert.Equal(_clock.GetUtcNow(), token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token desconhecido não é erro de quem chama e não grava nada")]
    public async Task Handle_WithUnknownToken_DoesNothing()
    {
        // Act
        await Handler().Handle(new LogoutCommand("nunca-emitido"), Cancel);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token nulo não estoura e não grava nada")]
    public async Task Handle_WithNullToken_DoesNothing()
    {
        // Act
        await Handler().Handle(new LogoutCommand(null!), Cancel);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token já revogado não é revogado de novo nem grava")]
    public async Task Handle_WithAnAlreadyRevokedToken_DoesNothing()
    {
        // Arrange
        var token = GivenAStoredToken();
        var revokedAt = Start.AddMinutes(1);
        token.Revoke(revokedAt);

        // Act
        await Handler().Handle(new LogoutCommand(Raw), Cancel);

        // Assert
        Assert.Equal(revokedAt, token.RevokedAt);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um token vencido não é revogado nem grava")]
    public async Task Handle_WithAnExpiredToken_DoesNothing()
    {
        // Arrange
        var token = GivenAStoredToken(issuedAt: Start.AddDays(-31));

        // Act
        await Handler().Handle(new LogoutCommand(Raw), Cancel);

        // Assert
        Assert.Null(token.RevokedAt);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Se outra requisição revogou o token nesse meio-tempo, o logout não falha")]
    public async Task Handle_WhenTheTokenWasRevokedMeanwhile_DoesNotThrow()
    {
        // Arrange
        GivenAStoredToken();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException(new InvalidOperationException()));

        // Act & Assert
        await Handler().Handle(new LogoutCommand(Raw), Cancel);
    }
}
