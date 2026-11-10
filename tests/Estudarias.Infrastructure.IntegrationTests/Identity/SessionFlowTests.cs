using Estudarias.Domain.Common.Exceptions;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Identity;

/// <summary>
/// Renovação da sessão e logout, de ponta a ponta contra o PostgreSQL real: é aqui que se prova a rotação atômica e a revogação da
/// família, que só o banco de verdade garante. Os requisitos citados são os do SRS do módulo (#48).
/// </summary>
[Collection(PostgresCollection.Name)]
public class SessionFlowTests(PostgresFixture postgres) : AuthenticationTests(postgres)
{
    private static async Task AssertInvalidRefreshToken(Task<Estudarias.Application.Identity.AuthPayload> attempt)
    {
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => attempt);
        Assert.Equal("identity.refreshToken.invalid", exception.Code);
    }

    [Fact(DisplayName = "RF-06 / RN-06: renovar revoga o token usado, que aponta o sucessor, na mesma família, e o sucessor renova de novo")]
    public async Task Refresh_RotatesTheTokenWithinTheSameFamily()
    {
        // Arrange
        var first = await RegisterAsync();

        // Act
        var second = await RefreshAsync(first.RefreshToken);
        var third = await RefreshAsync(second.RefreshToken);

        // Assert
        Assert.True(await ScalarAsync<bool>(
            $"SELECT revoked_at IS NOT NULL FROM refresh_token WHERE token_hash = '{HashOf(first.RefreshToken)}'"));
        Assert.True(await ScalarAsync<bool>(
            $"SELECT replaced_by_token_id = (SELECT id FROM refresh_token WHERE token_hash = '{HashOf(second.RefreshToken)}') " +
            $"FROM refresh_token WHERE token_hash = '{HashOf(first.RefreshToken)}'"));
        Assert.Equal(await FamilyOfAsync(first.RefreshToken), await FamilyOfAsync(second.RefreshToken));
        Assert.Equal(await FamilyOfAsync(first.RefreshToken), await FamilyOfAsync(third.RefreshToken));
        Assert.Equal(1, await CountAsync("refresh_token", "revoked_at IS NULL"));
        Assert.Equal(3, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "RF-06: cada renovação abre uma janela nova de 30 dias")]
    public async Task Refresh_OpensANewThirtyDayWindow()
    {
        // Arrange
        var first = await RegisterAsync();
        Clock.Advance(TimeSpan.FromDays(20));

        // Act
        var second = await RefreshAsync(first.RefreshToken);
        Clock.Advance(TimeSpan.FromDays(20));
        var third = await RefreshAsync(second.RefreshToken);

        // Assert: passaram 40 dias do cadastro, e a sessão continua viva porque cada uso renova a janela.
        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
        Assert.True(await ScalarAsync<bool>(
            $"SELECT expires_at = (SELECT expires_at FROM refresh_token WHERE token_hash = '{HashOf(first.RefreshToken)}') + interval '40 days' " +
            $"FROM refresh_token WHERE token_hash = '{HashOf(third.RefreshToken)}'"));
    }

    [Fact(DisplayName = "RN-07 / FA-06: um token já usado que reaparece revoga a família inteira, inclusive o sucessor")]
    public async Task Refresh_WithAnAlreadyUsedToken_RevokesTheWholeFamily()
    {
        // Arrange
        var first = await RegisterAsync();
        var second = await RefreshAsync(first.RefreshToken);
        var family = await FamilyOfAsync(first.RefreshToken);

        // Act: o token já usado reaparece, como se tivesse vazado.
        await AssertInvalidRefreshToken(RefreshAsync(first.RefreshToken));

        // Assert: a família inteira ficou revogada, então o sucessor legítimo também já não renova.
        Assert.Equal(0, await CountAsync("refresh_token", $"family_id = '{family}' AND revoked_at IS NULL"));
        await AssertInvalidRefreshToken(RefreshAsync(second.RefreshToken));
    }

    [Fact(DisplayName = "RN-07: o reuso numa família não afeta as sessões de outros dispositivos")]
    public async Task Refresh_WithAnAlreadyUsedToken_DoesNotTouchOtherFamilies()
    {
        // Arrange: a Ana tem dois dispositivos, cada um na sua família.
        var phone = await RegisterAsync();
        var tablet = await LoginAsync();
        var phoneRenewed = await RefreshAsync(phone.RefreshToken);

        // Act: o token antigo do celular reaparece.
        await AssertInvalidRefreshToken(RefreshAsync(phone.RefreshToken));

        // Assert: o celular perdeu a sessão; o tablet segue renovando.
        await AssertInvalidRefreshToken(RefreshAsync(phoneRenewed.RefreshToken));
        var tabletRenewed = await RefreshAsync(tablet.RefreshToken);
        Assert.NotEqual(tablet.RefreshToken, tabletRenewed.RefreshToken);
    }

    [Fact(DisplayName = "RN-08 / FA-08: renovações simultâneas do mesmo token: só uma vence, as outras recebem token inválido")]
    public async Task Refresh_WithConcurrentRenewalsOfTheSameToken_OnlyOneWins()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => AttemptAsync(() => RefreshAsync(session.RefreshToken))));

        // Assert: exatamente uma renovação passou, quer as outras tenham perdido a corrida no banco ou chegado depois.
        Assert.Single(outcomes, outcome => outcome.Succeeded);
        var failures = outcomes.Where(outcome => !outcome.Succeeded).ToList();
        Assert.Equal(7, failures.Count);
        Assert.All(failures, failure =>
            Assert.Equal("identity.refreshToken.invalid", Assert.IsType<DomainAuthenticationException>(failure.Error).Code));
        Assert.True(await ScalarAsync<bool>(
            $"SELECT revoked_at IS NOT NULL FROM refresh_token WHERE token_hash = '{HashOf(session.RefreshToken)}'"));
    }

    [Fact(DisplayName = "FA-05: um token vencido é recusado, e não é revogado nem gera sucessor")]
    public async Task Refresh_WithAnExpiredToken_IsRejectedWithoutRevokingIt()
    {
        // Arrange
        var session = await RegisterAsync();
        Clock.Advance(TimeSpan.FromDays(31));

        // Act & Assert
        await AssertInvalidRefreshToken(RefreshAsync(session.RefreshToken));
        Assert.Equal(1, await CountAsync("refresh_token"));
        Assert.Equal(1, await CountAsync("refresh_token", "revoked_at IS NULL"));
    }

    [Theory(DisplayName = "FA-05: um token desconhecido ou vazio é recusado")]
    [InlineData("nunca-emitido")]
    [InlineData("")]
    public async Task Refresh_WithAnUnknownToken_IsRejected(string token)
    {
        // Act & Assert
        await AssertInvalidRefreshToken(RefreshAsync(token));
    }

    [Fact(DisplayName = "FA-07: uma conta desabilitada não renova a sessão, e o token não é revogado")]
    public async Task Refresh_WithADisabledAccount_IsRejected()
    {
        // Arrange
        var session = await RegisterAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act & Assert
        await AssertInvalidRefreshToken(RefreshAsync(session.RefreshToken));
        Assert.Equal(1, await CountAsync("refresh_token", "revoked_at IS NULL"));
    }

    [Fact(DisplayName = "RF-07: o logout revoga o token daquele dispositivo, e ele não renova mais")]
    public async Task Logout_RevokesTheToken()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        await LogoutAsync(session.RefreshToken);

        // Assert: revogado por logout, sem sucessor.
        Assert.True(await ScalarAsync<bool>(
            $"SELECT revoked_at IS NOT NULL AND replaced_by_token_id IS NULL FROM refresh_token WHERE token_hash = '{HashOf(session.RefreshToken)}'"));
        await AssertInvalidRefreshToken(RefreshAsync(session.RefreshToken));
    }

    [Fact(DisplayName = "RF-07: o logout de um dispositivo não encerra a sessão dos outros")]
    public async Task Logout_DoesNotEndTheOtherDevices()
    {
        // Arrange
        var phone = await RegisterAsync();
        var tablet = await LoginAsync();

        // Act
        await LogoutAsync(phone.RefreshToken);

        // Assert
        var renewed = await RefreshAsync(tablet.RefreshToken);
        Assert.NotEqual(tablet.RefreshToken, renewed.RefreshToken);
    }

    [Fact(DisplayName = "RN-16 / FA-09: repetir o logout, ou sair com um token desconhecido, não falha nem grava nada")]
    public async Task Logout_WithAnInactiveOrUnknownToken_DoesNothing()
    {
        // Arrange
        var session = await RegisterAsync();
        await LogoutAsync(session.RefreshToken);
        var revokedAt = await ScalarAsync<DateTime>(
            $"SELECT revoked_at FROM refresh_token WHERE token_hash = '{HashOf(session.RefreshToken)}'");

        // Act
        await LogoutAsync(session.RefreshToken);
        await LogoutAsync("nunca-emitido");
        await LogoutAsync("");

        // Assert
        Assert.Equal(revokedAt, await ScalarAsync<DateTime>(
            $"SELECT revoked_at FROM refresh_token WHERE token_hash = '{HashOf(session.RefreshToken)}'"));
        Assert.Equal(1, await CountAsync("refresh_token"));
    }
}
