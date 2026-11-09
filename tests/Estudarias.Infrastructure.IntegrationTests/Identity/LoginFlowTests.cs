using Estudarias.Domain.Common.Exceptions;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Identity;

/// <summary>Login, de ponta a ponta contra o PostgreSQL real. Os requisitos citados são os do SRS do módulo (#48).</summary>
[Collection(PostgresCollection.Name)]
public class LoginFlowTests(PostgresFixture postgres) : AuthenticationTests(postgres)
{
    [Fact(DisplayName = "RF-03: com as credenciais certas o login devolve a sessão da conta cadastrada")]
    public async Task Login_WithValidCredentials_ReturnsTheSession()
    {
        // Arrange
        var registered = await RegisterAsync("ana@example.com");

        // Act
        var payload = await LoginAsync("  ANA@example.com ");

        // Assert
        Assert.Equal(registered.User.Id, payload.User.Id);
        Assert.StartsWith("access-token-for-", payload.AccessToken, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", payload.RefreshToken);
        Assert.Equal(HashOf(payload.RefreshToken), await ScalarAsync<string>(
            $"SELECT token_hash FROM refresh_token WHERE token_hash = '{HashOf(payload.RefreshToken)}'"));
    }

    [Fact(DisplayName = "RF-05: cada login abre uma família nova de refresh token, uma por dispositivo")]
    public async Task Login_EachTimeOpensANewFamily()
    {
        // Arrange
        await RegisterAsync();

        // Act
        await LoginAsync();
        await LoginAsync();

        // Assert: o cadastro e os dois logins, cada um na sua família.
        Assert.Equal(3, await CountAsync("refresh_token"));
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(DISTINCT family_id) FROM refresh_token"));
    }

    [Theory(DisplayName = "RN-01 / FA-04: senha errada, e-mail desconhecido, malformado ou vazio falham todos com a mesma resposta, sem emitir nada")]
    [InlineData("ana@example.com", "Outra-senha-2@")]
    [InlineData("bia@example.com", StrongPassword)]
    [InlineData("isto-nao-e-email", StrongPassword)]
    [InlineData("ana@example.com", "")]
    public async Task Login_WithInvalidCredentials_ThrowsTheSameError(string email, string password)
    {
        // Arrange
        await RegisterAsync("ana@example.com");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => LoginAsync(email, password));
        Assert.Equal("identity.credentials.invalid", exception.Code);
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "RN-01: uma conta desabilitada não entra, nem com a senha certa, e recebe a mesma resposta")]
    public async Task Login_WithADisabledAccount_ThrowsTheSameError()
    {
        // Arrange
        await RegisterAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => LoginAsync());
        Assert.Equal("identity.credentials.invalid", exception.Code);
    }

    [Fact(DisplayName = "RN-15: o login apaga os refresh tokens vencidos só do usuário que entrou")]
    public async Task Login_RemovesOnlyTheUsersExpiredTokens()
    {
        // Arrange: a Ana e a Bia se cadastram agora, e 31 dias depois os dois tokens estão vencidos.
        var ana = await RegisterAsync("ana@example.com");
        var bia = await RegisterAsync("bia@example.com");
        Clock.Advance(TimeSpan.FromDays(31));

        // Act
        var anaAgain = await LoginAsync("ana@example.com");

        // Assert: o da Ana sumiu, o da Bia continua (é de outro usuário) e há o login novo.
        Assert.Equal(0, await CountAsync("refresh_token", $"token_hash = '{HashOf(ana.RefreshToken)}'"));
        Assert.Equal(1, await CountAsync("refresh_token", $"token_hash = '{HashOf(bia.RefreshToken)}'"));
        Assert.Equal(1, await CountAsync("refresh_token", $"token_hash = '{HashOf(anaAgain.RefreshToken)}'"));
        Assert.Equal(2, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "RN-12: com o pepper rotacionado, o login refaz o hash da senha com o pepper novo e continua entrando")]
    public async Task Login_AfterAPepperRotation_RehashesThePassword()
    {
        // Arrange: duas contas com o pepper 1; depois a configuração passa a ter o pepper 2 como corrente, mantendo o 1.
        await RegisterAsync("ana@example.com");
        await RegisterAsync("bia@example.com");
        var hashBefore = await ScalarAsync<string>("SELECT password_hash FROM app_user WHERE email = 'ana@example.com'");
        using var rotated = BuildProvider(settings =>
        {
            settings["PasswordHashing:CurrentPepperVersion"] = "2";
            settings["PasswordHashing:Peppers:2"] = Samples.Pepper2;
        });

        // Act
        await LoginAsync("ana@example.com", provider: rotated);

        // Assert: só a conta que entrou foi refeita; a outra continua no pepper antigo, ainda válido.
        Assert.Equal(2, await ScalarAsync<int>("SELECT password_pepper_version FROM app_user WHERE email = 'ana@example.com'"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT password_pepper_version FROM app_user WHERE email = 'bia@example.com'"));
        Assert.NotEqual(hashBefore, await ScalarAsync<string>("SELECT password_hash FROM app_user WHERE email = 'ana@example.com'"));
        await LoginAsync("ana@example.com", provider: rotated);
        await LoginAsync("bia@example.com", provider: rotated);
        Assert.Equal(2, await ScalarAsync<int>("SELECT password_pepper_version FROM app_user WHERE email = 'bia@example.com'"));
    }

    [Fact(DisplayName = "FA-10: um pepper aposentado cedo demais é erro de configuração, não senha errada")]
    public async Task Login_WhenThePepperWasRetiredTooEarly_ThrowsAConfigurationError()
    {
        // Arrange: a conta está no pepper 1, mas a configuração só tem o 2.
        await RegisterAsync();
        using var onlyPepper2 = BuildProvider(settings =>
        {
            settings["PasswordHashing:CurrentPepperVersion"] = "2";
            settings.Remove("PasswordHashing:Peppers:1");
            settings["PasswordHashing:Peppers:2"] = Samples.Pepper2;
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => LoginAsync(provider: onlyPepper2));
        Assert.Contains("Pepper version 1", exception.Message, StringComparison.Ordinal);
    }
}
