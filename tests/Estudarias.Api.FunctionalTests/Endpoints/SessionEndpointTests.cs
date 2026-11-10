using System.Net;
using System.Text;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// POST /auth/refresh e /auth/logout, por HTTP contra um PostgreSQL real: é aqui que se prova, pela API, a rotação, a revogação da
/// família e a corrida entre renovações. Os requisitos citados são os do SRS do módulo (#48).
/// </summary>
[Collection(PostgresCollection.Name)]
public class SessionEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    private Task<HttpResponseMessage> RefreshAsync(string? refreshToken) => PostAsync("/auth/refresh", new { refreshToken });

    private Task<HttpResponseMessage> LogoutAsync(string? refreshToken) => PostAsync("/auth/logout", new { refreshToken });

    [Fact(DisplayName = "RF-06 / RN-06: renovar responde 200 com um par novo; o token usado fica revogado, apontando o sucessor, na mesma família")]
    public async Task Refresh_RotatesTheSession()
    {
        // Arrange
        var first = await RegisterAsync();

        // Act
        var response = await RefreshAsync(RefreshTokenOf(first));
        var second = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(RefreshTokenOf(first), RefreshTokenOf(second));
        Assert.Equal(
            first.GetProperty("user").GetProperty("id").GetGuid(),
            second.GetProperty("user").GetProperty("id").GetGuid());
        Assert.True(await ScalarAsync<bool>(
            $"SELECT revoked_at IS NOT NULL FROM refresh_token WHERE token_hash = '{HashOf(RefreshTokenOf(first))}'"));
        Assert.True(await ScalarAsync<bool>(
            $"SELECT replaced_by_token_id = (SELECT id FROM refresh_token WHERE token_hash = '{HashOf(RefreshTokenOf(second))}') " +
            $"FROM refresh_token WHERE token_hash = '{HashOf(RefreshTokenOf(first))}'"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(DISTINCT family_id) FROM refresh_token"));
        Assert.Equal(200, (int)(await RefreshAsync(RefreshTokenOf(second))).StatusCode);
    }

    [Fact(DisplayName = "RF-06 / RF-09: o access token da renovação abre um endpoint protegido")]
    public async Task Refresh_ReturnsATokenTheApiAccepts()
    {
        // Arrange
        var first = await RegisterAsync();
        var second = await JsonAsync(await RefreshAsync(RefreshTokenOf(first)));

        // Act
        var response = await GetWithTokenAsync("/__test/protected", AccessTokenOf(second));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "RN-07 / FA-06: um token já usado que reaparece é 401 e revoga a família inteira, inclusive o sucessor")]
    public async Task Refresh_WithAnAlreadyUsedToken_Returns401AndRevokesTheFamily()
    {
        // Arrange
        var first = await RegisterAsync();
        var second = await JsonAsync(await RefreshAsync(RefreshTokenOf(first)));

        // Act: o token já usado reaparece, como se tivesse vazado.
        var reuse = await RefreshAsync(RefreshTokenOf(first));

        // Assert
        await AssertProblemAsync(reuse, HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
        Assert.Equal(0, await CountAsync("refresh_token", "revoked_at IS NULL"));
        await AssertProblemAsync(await RefreshAsync(RefreshTokenOf(second)), HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
    }

    [Fact(DisplayName = "RN-08 / FA-08: renovações simultâneas do mesmo token: uma responde 200, as outras 401")]
    public async Task Refresh_WithConcurrentRenewalsOfTheSameToken_OnlyOneWins()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => RefreshAsync(RefreshTokenOf(session))));

        // Assert: exatamente uma passou, quer as outras tenham perdido a corrida no banco ou chegado depois.
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var failures = responses.Where(response => response.StatusCode == HttpStatusCode.Unauthorized).ToList();
        Assert.Equal(5, failures.Count);
        foreach (var failure in failures)
        {
            Assert.Equal("identity.refreshToken.invalid", (await JsonAsync(failure)).GetProperty("code").GetString());
        }
    }

    [Fact(DisplayName = "FA-05: um token desconhecido ou um corpo sem o token são 401, com o mesmo código")]
    public async Task Refresh_WithAnUnknownOrMissingToken_Returns401()
    {
        // Act
        var unknown = await RefreshAsync("nunca-emitido");
        var missing = await Client.PostAsync("/auth/refresh", new StringContent("{}", Encoding.UTF8, "application/json"), Cancel);

        // Assert
        await AssertProblemAsync(unknown, HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
        await AssertProblemAsync(missing, HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
    }

    [Fact(DisplayName = "FA-05: um token vencido (31 dias depois) é 401, sem ser revogado")]
    public async Task Refresh_WithAnExpiredToken_Returns401()
    {
        // Arrange
        var session = await RegisterAsync();
        Factory.Clock.Advance(TimeSpan.FromDays(31));

        // Act
        var response = await RefreshAsync(RefreshTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
        Assert.Equal(1, await CountAsync("refresh_token", "revoked_at IS NULL"));
    }

    [Fact(DisplayName = "FA-07: uma conta desabilitada não renova a sessão, e o token não é revogado")]
    public async Task Refresh_WithADisabledAccount_Returns401()
    {
        // Arrange
        var session = await RegisterAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act
        var response = await RefreshAsync(RefreshTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
        Assert.Equal(1, await CountAsync("refresh_token", "revoked_at IS NULL"));
    }

    [Fact(DisplayName = "RF-07: o logout responde 204, revoga o token daquele dispositivo e ele não renova mais")]
    public async Task Logout_Returns204AndRevokesTheToken()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var response = await LogoutAsync(RefreshTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await ScalarAsync<bool>(
            $"SELECT revoked_at IS NOT NULL AND replaced_by_token_id IS NULL FROM refresh_token WHERE token_hash = '{HashOf(RefreshTokenOf(session))}'"));
        await AssertProblemAsync(await RefreshAsync(RefreshTokenOf(session)), HttpStatusCode.Unauthorized, "identity.refreshToken.invalid");
    }

    [Fact(DisplayName = "RF-07: o logout de um dispositivo não encerra a sessão dos outros")]
    public async Task Logout_DoesNotEndTheOtherDevices()
    {
        // Arrange
        var phone = await RegisterAsync();
        var tablet = await JsonAsync(await PostAsync("/auth/login", new { email = "ana@example.com", password = StrongPassword }));

        // Act
        await LogoutAsync(RefreshTokenOf(phone));

        // Assert
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(RefreshTokenOf(tablet))).StatusCode);
    }

    [Fact(DisplayName = "RN-16 / FA-09: repetir o logout, ou sair com um token desconhecido ou ausente, também responde 204")]
    public async Task Logout_WithAnInactiveUnknownOrMissingToken_Returns204()
    {
        // Arrange
        var session = await RegisterAsync();
        await LogoutAsync(RefreshTokenOf(session));

        // Act
        var again = await LogoutAsync(RefreshTokenOf(session));
        var unknown = await LogoutAsync("nunca-emitido");
        var missing = await Client.PostAsync("/auth/logout", new StringContent("{}", Encoding.UTF8, "application/json"), Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, missing.StatusCode);
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "FA-03: um JSON malformado no logout é 400")]
    public async Task Logout_WithMalformedJson_Returns400()
    {
        // Act
        var response = await Client.PostAsync("/auth/logout", new StringContent("{isto-nao-e-json", Encoding.UTF8, "application/json"), Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
