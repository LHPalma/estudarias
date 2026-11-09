using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>POST /auth/login, por HTTP contra um PostgreSQL real. Os requisitos citados são os do SRS do módulo (#48).</summary>
[Collection(PostgresCollection.Name)]
public class LoginEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    [Fact(DisplayName = "RF-03: o login responde 200 com uma sessão nova da conta, em outra família de refresh token")]
    public async Task Login_WithValidCredentials_Returns200WithANewSession()
    {
        // Arrange
        var registered = await RegisterAsync("ana@example.com");

        // Act
        var response = await PostAsync("/auth/login", new { email = "  ANA@Example.com ", password = StrongPassword });
        var session = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            registered.GetProperty("user").GetProperty("id").GetGuid(),
            session.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("ana@example.com", session.GetProperty("user").GetProperty("email").GetString());
        Assert.NotEqual(RefreshTokenOf(registered), RefreshTokenOf(session));
        Assert.Equal(2, await CountAsync("refresh_token"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(DISTINCT family_id) FROM refresh_token"));
    }

    [Fact(DisplayName = "RF-03 / RF-09: o access token do login abre um endpoint protegido")]
    public async Task Login_ReturnsATokenTheApiAccepts()
    {
        // Arrange
        var registered = await RegisterAsync();
        var session = await JsonAsync(await PostAsync("/auth/login", new { email = "ana@example.com", password = StrongPassword }));

        // Act
        var response = await GetWithTokenAsync("/__test/protected", AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            registered.GetProperty("user").GetProperty("id").GetGuid(),
            (await JsonAsync(response)).GetProperty("userId").GetGuid());
    }

    [Theory(DisplayName = "RN-01 / FA-04: senha errada, e-mail desconhecido, malformado ou vazio recebem a mesma resposta 401, que não diz qual parte falhou")]
    [InlineData("ana@example.com", "Outra-senha-2@")]
    [InlineData("bia@example.com", StrongPassword)]
    [InlineData("isto-nao-e-email", StrongPassword)]
    [InlineData("ana@example.com", "")]
    [InlineData("", "")]
    public async Task Login_WithInvalidCredentials_Returns401WithTheSameResponse(string email, string password)
    {
        // Arrange
        await RegisterAsync("ana@example.com");

        // Act
        var response = await PostAsync("/auth/login", new { email, password });

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.credentials.invalid");
        Assert.Equal("E-mail or password not valid.", problem.GetProperty("detail").GetString());
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "RN-01: campos ausentes recebem a mesma resposta 401 de credenciais inválidas")]
    public async Task Login_WithMissingFields_Returns401()
    {
        // Act
        var response = await Client.PostAsync("/auth/login", new StringContent("{}", Encoding.UTF8, "application/json"), Cancel);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.credentials.invalid");
    }

    [Fact(DisplayName = "RN-01: uma conta desabilitada não entra, nem com a senha certa, e recebe a mesma resposta")]
    public async Task Login_WithADisabledAccount_Returns401()
    {
        // Arrange
        await RegisterAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act
        var response = await PostAsync("/auth/login", new { email = "ana@example.com", password = StrongPassword });

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.credentials.invalid");
    }

    [Fact(DisplayName = "RF-09: o login é aberto, e um token velho e inválido no cabeçalho não o impede")]
    public async Task Login_IgnoresAnInvalidBearerHeader()
    {
        // Arrange
        await RegisterAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { email = "ana@example.com", password = StrongPassword }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "um-token-velho-que-nao-vale");

        // Act
        var response = await Client.SendAsync(request, Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
