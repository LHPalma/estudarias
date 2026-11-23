using System.Net;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// O CORS por HTTP: o Expo aberto na web (um navegador) só chama a API se o preflight do <c>OPTIONS</c> for respondido, sem token, com
/// a origem liberada. Em Development qualquer origem é aceita; nos demais ambientes, só as da configuração.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CorsEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    private const string ExpoWeb = "http://localhost:8081";
    private const string Pepper = "production-pepper-1-0123456789-abcdefghijk";

    private static void ProductionSettings(Dictionary<string, string?> settings)
    {
        settings["PasswordHashing:CurrentPepperVersion"] = "1";
        settings["PasswordHashing:Peppers:1"] = Pepper;
    }

    private static Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin, string path = "/trail")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return client.SendAsync(request, Cancel);
    }

    private static string? AllowedOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;

    [Fact(DisplayName = "Em Development, o preflight do Expo web é respondido sem token, com o cabeçalho Authorization liberado")]
    public async Task Preflight_FromTheExpoWebOrigin_IsAllowedWithoutAToken()
    {
        // Act
        var response = await PreflightAsync(Client, ExpoWeb);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", AllowedOrigin(response));
        Assert.Contains("authorization", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GET", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Methods")), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"), "o app manda o token no Authorization, não cookie");
    }

    [Fact(DisplayName = "O preflight do cadastro também passa, para o app web conseguir entrar")]
    public async Task Preflight_ForTheRegistration_IsAllowed()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/auth/register");
        request.Headers.Add("Origin", ExpoWeb);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        // Act
        var response = await Client.SendAsync(request, Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", AllowedOrigin(response));
    }

    [Fact(DisplayName = "A resposta de uma chamada com token também leva a origem liberada")]
    public async Task Trail_FromTheExpoWebOrigin_CarriesTheAllowOriginHeader()
    {
        // Arrange
        var session = await RegisterAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/trail");
        request.Headers.Add("Origin", ExpoWeb);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessTokenOf(session));

        // Act
        var response = await Client.SendAsync(request, Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", AllowedOrigin(response));
    }

    [Fact(DisplayName = "Em Development, qualquer origem é aceita: a porta do Expo web varia")]
    public async Task Preflight_InDevelopment_AllowsAnyOrigin()
    {
        // Act
        var response = await PreflightAsync(Client, "http://localhost:8082");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", AllowedOrigin(response));
    }

    [Fact(DisplayName = "Em produção sem origens configuradas, nenhuma origem é liberada")]
    public async Task Preflight_InProductionWithoutConfiguration_AllowsNoOrigin()
    {
        // Arrange
        await using var factory = new DatabaseApiFactory(ConnectionString, "Production", ProductionSettings);
        using var client = factory.CreateClient();

        // Act
        var response = await PreflightAsync(client, ExpoWeb);

        // Assert
        Assert.Null(AllowedOrigin(response));
    }

    [Fact(DisplayName = "Em produção, só a origem configurada é liberada, e o Expo web de desenvolvimento não é")]
    public async Task Preflight_InProduction_AllowsOnlyTheConfiguredOrigin()
    {
        // Arrange
        await using var factory = new DatabaseApiFactory(ConnectionString, "Production", settings =>
        {
            ProductionSettings(settings);
            settings["Cors:AllowedOrigins:0"] = "https://app.estudarias.com.br";
        });
        using var client = factory.CreateClient();

        // Act
        var allowed = await PreflightAsync(client, "https://app.estudarias.com.br");
        var other = await PreflightAsync(client, ExpoWeb);

        // Assert
        Assert.Equal("https://app.estudarias.com.br", AllowedOrigin(allowed));
        Assert.Null(AllowedOrigin(other));
    }
}
