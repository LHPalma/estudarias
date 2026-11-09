using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Microsoft.IdentityModel.Tokens;

namespace Estudarias.Api.FunctionalTests.Security;

/// <summary>
/// O pipeline de autenticação da API inteira, por HTTP: login obrigatório por padrão (RF-09) e a validação do access token (RF-04).
/// Os requisitos citados são os do SRS do módulo (#48).
/// </summary>
public class AuthenticationPipelineTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private Task<HttpResponseMessage> GetAsync(string path, string? token = null, string scheme = "Bearer")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        return factory.CreateClient().SendAsync(request, Cancel);
    }

    private static string Challenge(HttpResponseMessage response) =>
        string.Join(" ", response.Headers.WwwAuthenticate.Select(header => header.ToString()));

    [Fact(DisplayName = "RF-09: o /health fica aberto, sem token")]
    public async Task Health_WithoutToken_IsOpen()
    {
        // Act
        var response = await GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "RF-09: um endpoint sem nenhum [Authorize] já nasce protegido pela política padrão")]
    public async Task Protected_WithoutToken_Returns401WithABearerChallenge()
    {
        // Act
        var response = await GetAsync("/__test/protected");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", Challenge(response), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "RF-09: quem precisa ser aberto declara [AllowAnonymous], e a política padrão respeita")]
    public async Task Open_WithAnAllowAnonymousEndpoint_IsOpen()
    {
        // Act
        var response = await GetAsync("/__test/open");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "RF-04 / ICurrentUser: com um token válido o endpoint responde, e o usuário corrente é o da claim sub")]
    public async Task Protected_WithAValidToken_Returns200AndTheCurrentUser()
    {
        // Arrange
        var ana = ApiFactory.Student("ana@example.com");
        var bia = ApiFactory.Student("bia@example.com");

        // Act
        var forAna = await GetAsync("/__test/protected", factory.IssueToken(ana));
        var forBia = await GetAsync("/__test/protected", factory.IssueToken(bia));

        // Assert
        Assert.Equal(HttpStatusCode.OK, forAna.StatusCode);
        Assert.Equal(ana.Id, (await forAna.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("userId").GetGuid());
        Assert.Equal(bia.Id, (await forBia.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("userId").GetGuid());
    }

    [Fact(DisplayName = "FA-12: uma rota que não existe responde 401 a quem não tem token, e 404 a quem tem")]
    public async Task UnknownRoute_ResponseDependsOnTheToken()
    {
        // Act
        var anonymous = await GetAsync("/nao-existe");
        var authenticated = await GetAsync("/nao-existe", factory.IssueToken(ApiFactory.Student()));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, authenticated.StatusCode);
    }

    [Fact(DisplayName = "FA-12: um token vencido é recusado, dizendo que expirou")]
    public async Task Protected_WithAnExpiredToken_Returns401()
    {
        // Arrange
        var token = ApiFactory.IssueTokenAt(ApiFactory.Student(), DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var response = await GetAsync("/__test/protected", token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("expired", Challenge(response), StringComparison.OrdinalIgnoreCase);
    }

    [Theory(DisplayName = "FA-12: a tolerância de relógio é de 30 segundos: vencido há 10 s ainda vale, vencido há 2 min não")]
    [InlineData(10, HttpStatusCode.OK)]
    [InlineData(120, HttpStatusCode.Unauthorized)]
    public async Task Protected_AppliesAThirtySecondClockSkew(int secondsExpired, HttpStatusCode expected)
    {
        // Arrange: o token vale 15 minutos, então emitido há 15 min + N s ele venceu há N s.
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-15).AddSeconds(-secondsExpired);
        var token = ApiFactory.IssueTokenAt(ApiFactory.Student(), issuedAt);

        // Act
        var response = await GetAsync("/__test/protected", token);

        // Assert
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact(DisplayName = "FA-12: um token assinado com outra chave é recusado")]
    public async Task Protected_WithATokenSignedWithAnotherKey_Returns401()
    {
        // Arrange
        var token = ApiFactory.IssueTokenAt(
            ApiFactory.Student(), DateTimeOffset.UtcNow, options => options.SigningKey = ApiFactory.OtherSigningKey);

        // Act & Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/__test/protected", token)).StatusCode);
    }

    [Theory(DisplayName = "FA-12: um token de outro emissor ou de outra audiência é recusado")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public async Task Protected_WithAnotherIssuerOrAudience_Returns401(string which)
    {
        // Arrange
        var token = ApiFactory.IssueTokenAt(ApiFactory.Student(), DateTimeOffset.UtcNow, options =>
        {
            if (which == "issuer")
            {
                options.Issuer = "Outro";
            }
            else
            {
                options.Audience = "Outro";
            }
        });

        // Act & Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/__test/protected", token)).StatusCode);
    }

    [Fact(DisplayName = "FA-12: um token assinado com outro algoritmo (HS512), mesmo com a chave certa, é recusado")]
    public async Task Protected_WithADifferentAlgorithm_Returns401()
    {
        // Arrange
        var token = CraftedTokens.Create(algorithm: SecurityAlgorithms.HmacSha512);

        // Act & Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/__test/protected", token)).StatusCode);
    }

    [Fact(DisplayName = "FA-12: um token sem assinatura (alg none) é recusado")]
    public async Task Protected_WithAnUnsignedToken_Returns401()
    {
        // Arrange
        var token = CraftedTokens.Create(signingKey: null);

        // Act & Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/__test/protected", token)).StatusCode);
    }

    [Fact(DisplayName = "FA-12: um token adulterado depois de assinado é recusado")]
    public async Task Protected_WithATamperedToken_Returns401()
    {
        // Arrange: troca o id da conta no corpo do token, mantendo a assinatura de antes.
        var parts = factory.IssueToken(ApiFactory.Student()).Split('.');
        var forged = Base64UrlEncoder.Encode(
            $$"""{"sub":"{{Guid.CreateVersion7()}}","email":"ana@example.com","role":"Student","iss":"Estudarias","aud":"Estudarias","exp":{{DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()}}}""");
        var token = $"{parts[0]}.{forged}.{parts[2]}";

        // Act & Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/__test/protected", token)).StatusCode);
    }

    [Theory(DisplayName = "FA-12: lixo no lugar do token, ou outro esquema de autenticação, é recusado")]
    [InlineData("isto-nao-e-um-jwt", "Bearer")]
    [InlineData("a.b.c", "Bearer")]
    [InlineData("dXNlcjpzZW5oYQ==", "Basic")]
    public async Task Protected_WithGarbageOrAnotherScheme_Returns401(string value, string scheme)
    {
        // Act & Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/__test/protected", value, scheme)).StatusCode);
    }

    [Fact(DisplayName = "RF-04: o papel do token vale para a autorização: aluno entra, outro papel é proibido, sem token é 401")]
    public async Task StudentOnly_DependsOnTheRoleClaim()
    {
        // Act
        var student = await GetAsync("/__test/student-only", factory.IssueToken(ApiFactory.Student()));
        var otherRole = await GetAsync("/__test/student-only", CraftedTokens.Create(role: "Admin"));
        var anonymous = await GetAsync("/__test/student-only");

        // Assert
        Assert.Equal(HttpStatusCode.OK, student.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherRole.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
