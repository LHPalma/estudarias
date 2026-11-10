using System.Net;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>O que a API devolve quando o erro não é do cliente: um 500 genérico, sem detalhe técnico (RNF-06).</summary>
[Collection(PostgresCollection.Name)]
public class UnexpectedErrorEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    private const string Pepper1 = "production-pepper-1-0123456789-abcdefghijk";
    private const string Pepper2 = "production-pepper-2-9876543210-zyxwvutsrqp";

    [Fact(DisplayName = "FA-10 / RNF-06: um pepper aposentado cedo demais é erro de configuração: 500 genérico, sem a mensagem técnica nem código de erro")]
    public async Task Login_WhenThePepperWasRetiredTooEarly_Returns500WithoutLeakingDetails()
    {
        // Arrange: a conta nasce com o pepper 1; depois a configuração de produção passa a ter só o pepper 2.
        await using var before = new DatabaseApiFactory(ConnectionString, "Production", settings =>
        {
            settings["PasswordHashing:CurrentPepperVersion"] = "1";
            settings["PasswordHashing:Peppers:1"] = Pepper1;
        });
        using var beforeClient = before.CreateClient();
        await RegisterAsync(client: beforeClient);

        await using var after = new DatabaseApiFactory(ConnectionString, "Production", settings =>
        {
            settings["PasswordHashing:CurrentPepperVersion"] = "2";
            settings["PasswordHashing:Peppers:2"] = Pepper2;
        });
        using var afterClient = after.CreateClient();

        // Act
        var response = await PostAsync("/auth/login", new { email = "ana@example.com", password = StrongPassword }, afterClient);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        var problem = JsonDocument.Parse(body).RootElement;

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(problem.TryGetProperty("code", out _), "um erro inesperado não tem código de erro de domínio");
        Assert.DoesNotContain("Pepper", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Pepper1, body, StringComparison.Ordinal);
    }
}
