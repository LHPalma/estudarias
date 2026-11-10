using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>POST /auth/register, por HTTP contra um PostgreSQL real. Os requisitos citados são os do SRS do módulo (#48).</summary>
[Collection(PostgresCollection.Name)]
public class RegisterEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    [Fact(DisplayName = "RF-01: o cadastro responde 201 com a sessão (os dois tokens, a validade do access e o usuário) e grava aluno e conta ligados")]
    public async Task Register_WithValidData_Returns201WithTheSession()
    {
        // Act
        var response = await PostAsync("/auth/register", new { name = "  Ana  ", email = "  Ana@Example.COM ", password = StrongPassword });
        var session = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
        Assert.Equal(3, AccessTokenOf(session).Split('.').Length);
        Assert.Equal(Factory.Clock.GetUtcNow().AddMinutes(15), session.GetProperty("accessTokenExpiresAt").GetDateTimeOffset());
        Assert.Matches("^[0-9a-f]{64}$", RefreshTokenOf(session));

        var user = session.GetProperty("user");
        Assert.Equal("ana@example.com", user.GetProperty("email").GetString());
        Assert.Equal("Student", user.GetProperty("role").GetString());
        Assert.Equal(user.GetProperty("id").GetGuid(), await ScalarAsync<Guid>("SELECT id FROM app_user"));
        Assert.Equal(user.GetProperty("studentId").GetGuid(), await ScalarAsync<Guid>("SELECT id FROM student"));
        Assert.Equal("Ana", await ScalarAsync<string>("SELECT name FROM student"));
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "RF-01 / RF-05: o refresh token da resposta é gravado só pelo hash, e a senha vira um hash argon2id")]
    public async Task Register_StoresOnlyHashes()
    {
        // Act
        var session = await RegisterAsync();

        // Assert
        Assert.Equal(HashOf(RefreshTokenOf(session)), await ScalarAsync<string>("SELECT token_hash FROM refresh_token"));
        Assert.Equal(0, await CountAsync("refresh_token", $"token_hash = '{RefreshTokenOf(session)}'"));
        var hash = await ScalarAsync<string>("SELECT password_hash FROM app_user");
        Assert.StartsWith("$argon2id$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain(StrongPassword, hash, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "RF-04 / RF-09: o access token do cadastro abre um endpoint protegido, e o usuário corrente é a conta criada")]
    public async Task Register_ReturnsATokenTheApiAccepts()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var protectedResponse = await GetWithTokenAsync("/__test/protected", AccessTokenOf(session));
        var studentOnly = await GetWithTokenAsync("/__test/student-only", AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        Assert.Equal(
            session.GetProperty("user").GetProperty("id").GetGuid(),
            (await JsonAsync(protectedResponse)).GetProperty("userId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, studentOnly.StatusCode);
    }

    [Fact(DisplayName = "FA-01 / RN-02: e-mail já cadastrado, em qualquer caixa de letras, é 409 com o código identity.email.taken, e nada novo é gravado")]
    public async Task Register_WithTakenEmail_Returns409()
    {
        // Arrange
        await RegisterAsync("ana@example.com");

        // Act
        var response = await PostAsync("/auth/register", new { name = "Outra Ana", email = "  ANA@example.com ", password = StrongPassword });

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "identity.email.taken");
        Assert.Equal(1, await CountAsync("student"));
        Assert.Equal(1, await CountAsync("app_user"));
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "FA-02 / RN-14: cadastros simultâneos com o mesmo e-mail: um responde 201, os outros 409, e só uma conta fica gravada")]
    public async Task Register_WithConcurrentRegistrationsOfTheSameEmail_OnlyOneWins()
    {
        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(index => PostAsync("/auth/register", new { name = $"Ana {index}", email = "ana@example.com", password = StrongPassword })));

        // Assert
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Where(response => response.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(3, conflicts.Count);
        foreach (var conflict in conflicts)
        {
            Assert.Equal("identity.email.taken", (await JsonAsync(conflict)).GetProperty("code").GetString());
        }

        Assert.Equal(1, await CountAsync("student"));
        Assert.Equal(1, await CountAsync("app_user"));
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Theory(DisplayName = "RN-03 / FA-03: dado inválido é 400 com o código do erro, e nada é gravado")]
    [InlineData("Ana", "ana@example.com", "curta1!", "identity.password.tooShort")]
    [InlineData("Ana", "ana@example.com", "senha-sem-maiuscula-1!", "identity.password.noUppercase")]
    [InlineData("Ana", "ana@example.com", "SENHA-SEM-MINUSCULA-1!", "identity.password.noLowercase")]
    [InlineData("Ana", "ana@example.com", "Senha-sem-digito!", "identity.password.noDigit")]
    [InlineData("Ana", "ana@example.com", "SenhaSemEspecial1", "identity.password.noSpecial")]
    [InlineData("Ana", "isto-nao-e-email", StrongPassword, "identity.email.invalid")]
    [InlineData("   ", "ana@example.com", StrongPassword, "student.name.required")]
    public async Task Register_WithInvalidData_Returns400WithTheErrorCode(string name, string email, string password, string code)
    {
        // Act
        var response = await PostAsync("/auth/register", new { name, email, password });

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
        Assert.Equal(0, await CountAsync("student"));
        Assert.Equal(0, await CountAsync("app_user"));
        Assert.Equal(0, await CountAsync("refresh_token"));
    }

    [Theory(DisplayName = "FA-03: campos ausentes ou nulos recebem o mesmo formato de erro, com o código do domínio")]
    [InlineData("{}", "identity.email.invalid")]
    [InlineData("""{"email":null,"password":null,"name":null}""", "identity.email.invalid")]
    [InlineData("""{"email":"ana@example.com"}""", "identity.password.tooShort")]
    [InlineData("""{"email":"ana@example.com","password":"Senha-valida-1!"}""", "student.name.required")]
    public async Task Register_WithMissingFields_Returns400WithTheDomainCode(string json, string code)
    {
        // Act
        var response = await Client.PostAsync("/auth/register", new StringContent(json, Encoding.UTF8, "application/json"), Cancel);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
        Assert.Equal(0, await CountAsync("app_user"));
    }

    [Fact(DisplayName = "FA-03: um JSON malformado é 400, e um corpo que não é JSON é 415")]
    public async Task Register_WithABadBody_IsRejected()
    {
        // Act
        var malformed = await Client.PostAsync("/auth/register", new StringContent("{isto-nao-e-json", Encoding.UTF8, "application/json"), Cancel);
        var notJson = await Client.PostAsync("/auth/register", new StringContent("name=Ana", Encoding.UTF8, "text/plain"), Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJson.StatusCode);
        Assert.Equal(0, await CountAsync("app_user"));
    }

    [Fact(DisplayName = "RF-09: o cadastro é aberto, e um token inválido no cabeçalho não o impede")]
    public async Task Register_IgnoresAnInvalidBearerHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new { name = "Ana", email = "ana@example.com", password = StrongPassword }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "um-token-velho-que-nao-vale");

        // Act
        var response = await Client.SendAsync(request, Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
