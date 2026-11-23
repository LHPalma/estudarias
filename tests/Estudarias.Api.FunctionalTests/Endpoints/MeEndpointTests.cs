using System.Net;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// GET /me por HTTP contra um PostgreSQL real: o perfil de quem está logado, lido pela porta de leitura (ADR-002). O token vem do
/// cadastro, como o app o obteria, e a resposta é conferida com o banco.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MeEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    [Fact(DisplayName = "GET /me responde 200 com a conta e o aluno de quem está logado")]
    public async Task Me_ReturnsTheLoggedInProfile()
    {
        // Arrange
        var session = await RegisterAsync("Ana@Example.com", "Ana Souza");
        var user = session.GetProperty("user");

        // Act
        var response = await GetWithTokenAsync("/me", AccessTokenOf(session));
        var me = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(user.GetProperty("id").GetGuid(), me.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("ana@example.com", me.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("Student", me.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal(user.GetProperty("studentId").GetGuid(), me.GetProperty("student").GetProperty("id").GetGuid());
        Assert.Equal("Ana Souza", me.GetProperty("student").GetProperty("name").GetString());
    }

    [Fact(DisplayName = "O perfil traz o link self, com a URL absoluta do próprio /me")]
    public async Task Me_LinksToItself()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var me = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(session)));

        // Assert
        var link = me.GetProperty("links").EnumerateArray().Single(candidate => candidate.GetProperty("rel").GetString() == "self");
        Assert.Equal(["href", "method", "rel"], link.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("GET", link.GetProperty("method").GetString());
        Assert.Equal("http://localhost/me", link.GetProperty("href").GetString());
    }

    [Fact(DisplayName = "O perfil traz o link trail, com a URL absoluta da trilha")]
    public async Task Me_LinksToTheTrail()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var me = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(session)));

        // Assert
        var links = me.GetProperty("links").EnumerateArray().ToList();
        Assert.Equal(["self", "trail"], links.Select(link => link.GetProperty("rel").GetString()));
        var trail = links.Single(link => link.GetProperty("rel").GetString() == "trail");
        Assert.Equal("GET", trail.GetProperty("method").GetString());
        Assert.Equal("http://localhost/trail", trail.GetProperty("href").GetString());
    }

    [Fact(DisplayName = "O link self do perfil leva de volta ao mesmo perfil")]
    public async Task Me_SelfLink_ReturnsTheSameProfile()
    {
        // Arrange
        var session = await RegisterAsync();
        var me = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(session)));
        var self = me.GetProperty("links")[0].GetProperty("href").GetString()!;

        // Act
        var followed = await JsonAsync(await GetWithTokenAsync(self, AccessTokenOf(session)));

        // Assert
        Assert.Equal(me.GetRawText(), followed.GetRawText());
    }

    [Theory(DisplayName = "O grupo experimental da resposta é o que está gravado no aluno")]
    [InlineData("control", "Control")]
    [InlineData("diagnostic", "Diagnostic")]
    public async Task Me_ReturnsTheStoredExperimentalGroup(string stored, string expected)
    {
        // Arrange
        var session = await RegisterAsync();
        await ExecuteAsync($"UPDATE student SET experimental_group = '{stored}'");

        // Act
        var me = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(session)));

        // Assert
        Assert.Equal(expected, me.GetProperty("student").GetProperty("experimentalGroup").GetString());
    }

    [Fact(DisplayName = "A resposta tem só os campos do contrato: nada do hash da senha, do refresh token nem do progresso")]
    public async Task Me_ReturnsOnlyTheContractFields()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var me = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(session)));

        // Assert
        Assert.Equal(["links", "student", "user"], me.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["email", "id", "role"], me.GetProperty("user").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["experimentalGroup", "id", "name"], me.GetProperty("student").EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact(DisplayName = "Cada aluno logado vê o próprio perfil, e não o de outro")]
    public async Task Me_WithTwoStudents_ReturnsEachOwnProfile()
    {
        // Arrange
        var ana = await RegisterAsync("ana@example.com", "Ana");
        var bia = await RegisterAsync("bia@example.com", "Bia");

        // Act
        var anaMe = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(ana)));
        var biaMe = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(bia)));

        // Assert
        Assert.Equal(("ana@example.com", "Ana"), (anaMe.GetProperty("user").GetProperty("email").GetString(), anaMe.GetProperty("student").GetProperty("name").GetString()));
        Assert.Equal(("bia@example.com", "Bia"), (biaMe.GetProperty("user").GetProperty("email").GetString(), biaMe.GetProperty("student").GetProperty("name").GetString()));
    }

    [Fact(DisplayName = "O access token da renovação também abre o /me")]
    public async Task Me_AcceptsTheTokenFromARefresh()
    {
        // Arrange
        var first = await RegisterAsync();
        var second = await JsonAsync(await PostAsync("/auth/refresh", new { refreshToken = RefreshTokenOf(first) }));

        // Act
        var response = await GetWithTokenAsync("/me", AccessTokenOf(second));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Sem token o /me responde 401 com o desafio Bearer")]
    public async Task Me_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.GetAsync("/me", Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "Com um token assinado por outra chave o /me responde 401")]
    public async Task Me_WithATokenFromAnotherKey_Returns401()
    {
        // Arrange
        await RegisterAsync();
        var token = CraftedTokens.Create(signingKey: ApiFactory.OtherSigningKey);

        // Act
        var response = await GetWithTokenAsync("/me", token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Um token válido de uma conta que não existe responde 401 com identity.session.invalid")]
    public async Task Me_ForAnUnknownAccount_Returns401WithTheSessionCode()
    {
        // Arrange
        var token = CraftedTokens.Create(userId: Guid.CreateVersion7());

        // Act
        var response = await GetWithTokenAsync("/me", token);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "O token de uma conta desabilitada depois do login responde 401 com identity.session.invalid")]
    public async Task Me_ForADisabledAccount_Returns401WithTheSessionCode()
    {
        // Arrange
        var session = await RegisterAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act
        var response = await GetWithTokenAsync("/me", AccessTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
    }

    [Fact(DisplayName = "A resposta 401 não distingue conta inexistente de conta desabilitada")]
    public async Task Me_UnknownAndDisabledAccounts_GetTheSameProblem()
    {
        // Arrange
        var session = await RegisterAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act
        var disabled = await AssertProblemAsync(
            await GetWithTokenAsync("/me", AccessTokenOf(session)), HttpStatusCode.Unauthorized, "identity.session.invalid");
        var unknown = await AssertProblemAsync(
            await GetWithTokenAsync("/me", CraftedTokens.Create(userId: Guid.CreateVersion7())),
            HttpStatusCode.Unauthorized,
            "identity.session.invalid");

        // Assert
        Assert.Equal(disabled.GetProperty("detail").GetString(), unknown.GetProperty("detail").GetString());
        Assert.Equal(disabled.GetProperty("title").GetString(), unknown.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "O /me é só de leitura: nada é gravado no banco")]
    public async Task Me_WritesNothing()
    {
        // Arrange
        var session = await RegisterAsync();
        var before = await ScalarAsync<long>("SELECT (SELECT count(*) FROM student) + (SELECT count(*) FROM app_user) + (SELECT count(*) FROM refresh_token)");

        // Act
        await GetWithTokenAsync("/me", AccessTokenOf(session));
        await GetWithTokenAsync("/me", AccessTokenOf(session));

        // Assert
        Assert.Equal(before, await ScalarAsync<long>("SELECT (SELECT count(*) FROM student) + (SELECT count(*) FROM app_user) + (SELECT count(*) FROM refresh_token)"));
    }
}
