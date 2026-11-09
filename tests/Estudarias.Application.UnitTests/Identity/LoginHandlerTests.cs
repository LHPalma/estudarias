using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.UnitTests.Support;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Estudarias.Application.UnitTests.Identity;

public class LoginHandlerTests
{
    private const string Plain = "Senha-valida-1!";
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAccessTokenIssuer _accessTokens = Substitute.For<IAccessTokenIssuer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start);
    private readonly List<RefreshToken> _addedTokens = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public LoginHandlerTests()
    {
        _accessTokens.Issue(Arg.Any<User>()).Returns(new IssuedAccessToken("access-token", Start.AddMinutes(15)));
        _refreshTokens.When(repository => repository.Add(Arg.Any<RefreshToken>())).Do(call => _addedTokens.Add(call.Arg<RefreshToken>()));
    }

    private LoginHandler Handler() => new(_users, _refreshTokens, _hasher, _accessTokens, _unitOfWork, _clock);

    private User GivenAnAccount(User? user = null, string plain = Plain)
    {
        user ??= TestUsers.Active();
        _users.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(user.PasswordHash, plain).Returns(true);
        return user;
    }

    private async Task AssertInvalidCredentials(LoginCommand command)
    {
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => Handler().Handle(command, Cancel));
        Assert.Equal("identity.credentials.invalid", exception.Code);
        Assert.Empty(_addedTokens);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _accessTokens.DidNotReceive().Issue(Arg.Any<User>());
    }

    [Fact(DisplayName = "Com as credenciais certas o login devolve a sessão: os dois tokens, a validade do access e a conta")]
    public async Task Handle_WithValidCredentials_ReturnsTheSession()
    {
        // Arrange
        var user = GivenAnAccount();

        // Act
        var payload = await Handler().Handle(new LoginCommand("ana@example.com", Plain), Cancel);

        // Assert
        Assert.Equal("access-token", payload.AccessToken);
        Assert.Equal(Start.AddMinutes(15), payload.AccessTokenExpiresAt);
        Assert.Matches("^[0-9a-f]{64}$", payload.RefreshToken);
        Assert.Same(user, payload.User);
    }

    [Fact(DisplayName = "O refresh token é gravado só pelo hash, numa família nova, por 30 dias e para a conta certa")]
    public async Task Handle_StoresTheRefreshTokenByHashInANewFamily()
    {
        // Arrange
        var user = GivenAnAccount();

        // Act
        var first = await Handler().Handle(new LoginCommand("ana@example.com", Plain), Cancel);
        var second = await Handler().Handle(new LoginCommand("ana@example.com", Plain), Cancel);

        // Assert
        Assert.Equal(2, _addedTokens.Count);
        Assert.Equal(RefreshTokenSecret.Hash(first.RefreshToken), _addedTokens[0].TokenHash);
        Assert.NotEqual(first.RefreshToken, _addedTokens[0].TokenHash);
        Assert.Equal(user.Id, _addedTokens[0].UserId);
        Assert.Equal(Start.AddDays(30), _addedTokens[0].ExpiresAt);
        Assert.NotEqual(_addedTokens[0].FamilyId, _addedTokens[1].FamilyId);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }

    [Fact(DisplayName = "O login apaga os refresh tokens vencidos do usuário e grava tudo numa gravação só")]
    public async Task Handle_RemovesTheUsersExpiredTokensAndSavesOnce()
    {
        // Arrange
        var user = GivenAnAccount();

        // Act
        await Handler().Handle(new LoginCommand("ana@example.com", Plain), Cancel);

        // Assert
        Received.InOrder(() =>
        {
            _refreshTokens.RemoveExpiredAsync(user.Id, Start, Arg.Any<CancellationToken>());
            _refreshTokens.Add(Arg.Any<RefreshToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O e-mail é normalizado antes de procurar a conta")]
    public async Task Handle_NormalizesTheEmail()
    {
        // Arrange
        GivenAnAccount();

        // Act
        var payload = await Handler().Handle(new LoginCommand("  ANA@Example.com ", Plain), Cancel);

        // Assert
        Assert.Equal("ana@example.com", payload.User.Email);
    }

    [Fact(DisplayName = "Senha errada falha com credenciais inválidas, sem emitir nada")]
    public async Task Handle_WithWrongPassword_ThrowsInvalidCredentials()
    {
        // Arrange
        GivenAnAccount();

        // Act & Assert
        await AssertInvalidCredentials(new LoginCommand("ana@example.com", "Outra-senha-2@"));
    }

    [Fact(DisplayName = "E-mail desconhecido falha igual, e a senha é conferida contra o hash descartável")]
    public async Task Handle_WithUnknownEmail_ThrowsTheSameErrorAndStillVerifies()
    {
        // Act & Assert
        await AssertInvalidCredentials(new LoginCommand("bia@example.com", Plain));
        _hasher.Received(1).Verify(null, Plain);
    }

    [Fact(DisplayName = "E-mail malformado falha igual, sem consultar o banco, e a senha ainda é conferida")]
    public async Task Handle_WithMalformedEmail_ThrowsTheSameErrorAndStillVerifies()
    {
        // Act & Assert
        await AssertInvalidCredentials(new LoginCommand("isto-nao-e-email", Plain));
        await _users.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _hasher.Received(1).Verify(null, Plain);
    }

    [Fact(DisplayName = "Usuário desabilitado falha igual, mesmo com a senha certa")]
    public async Task Handle_WithInactiveUser_ThrowsTheSameError()
    {
        // Arrange
        GivenAnAccount(TestUsers.Inactive());

        // Act & Assert
        await AssertInvalidCredentials(new LoginCommand("ana@example.com", Plain));
    }

    [Fact(DisplayName = "Senha nula é tratada como senha errada, sem estourar")]
    public async Task Handle_WithNullPassword_ThrowsInvalidCredentials()
    {
        // Arrange
        GivenAnAccount();

        // Act & Assert
        await AssertInvalidCredentials(new LoginCommand("ana@example.com", null!));
    }

    [Fact(DisplayName = "Um hash de pepper antigo é refeito com o pepper corrente no login")]
    public async Task Handle_WhenThePepperRotated_RehashesThePassword()
    {
        // Arrange
        var user = GivenAnAccount();
        var newHash = PasswordHash.Create("$argon2id$v=19$m=19456,t=2,p=1$bm92bw$bm92bw", 2);
        _hasher.NeedsRehash(user.PasswordHash).Returns(true);
        _hasher.Hash(Arg.Is<Password>(password => password.Value == Plain)).Returns(newHash);

        // Act
        await Handler().Handle(new LoginCommand("ana@example.com", Plain), Cancel);

        // Assert
        Assert.Equal(newHash, user.PasswordHash);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um hash que está no pepper corrente não é refeito")]
    public async Task Handle_WhenThePepperIsCurrent_DoesNotRehash()
    {
        // Arrange
        var user = GivenAnAccount();
        var before = user.PasswordHash;

        // Act
        await Handler().Handle(new LoginCommand("ana@example.com", Plain), Cancel);

        // Assert
        _hasher.DidNotReceive().Hash(Arg.Any<Password>());
        Assert.Same(before, user.PasswordHash);
    }

    [Fact(DisplayName = "Uma senha que já não cumpre a política entra mesmo assim e mantém o hash antigo")]
    public async Task Handle_WhenThePasswordNoLongerMeetsThePolicy_KeepsTheOldHash()
    {
        // Arrange
        const string weak = "senha-antiga";
        var user = GivenAnAccount(plain: weak);
        var before = user.PasswordHash;
        _hasher.NeedsRehash(user.PasswordHash).Returns(true);

        // Act
        var payload = await Handler().Handle(new LoginCommand("ana@example.com", weak), Cancel);

        // Assert
        Assert.Same(before, user.PasswordHash);
        _hasher.DidNotReceive().Hash(Arg.Any<Password>());
        Assert.Same(user, payload.User);
    }
}
