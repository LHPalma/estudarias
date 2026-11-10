using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Npgsql;

namespace Estudarias.Api.FunctionalTests.Support;

/// <summary>
/// Base dos testes dos endpoints de autenticação: a API inteira por HTTP, sobre um PostgreSQL de verdade, um banco novo por teste. Os
/// testes conferem a resposta e também o banco direto.
/// </summary>
public abstract class AuthEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    protected const string StrongPassword = "Senha-valida-1!";

    private string _connectionString = "";

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    internal DatabaseApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _connectionString = await postgres.CreateDatabaseAsync();
        Factory = new DatabaseApiFactory(_connectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await postgres.DropDatabaseAsync(_connectionString);
    }

    /// <summary>A connection string do banco deste teste, para outra instância da API (outra configuração) usar o mesmo banco.</summary>
    internal string ConnectionString => _connectionString;

    protected Task<HttpResponseMessage> PostAsync(string path, object? body, HttpClient? client = null) =>
        (client ?? Client).PostAsJsonAsync(path, body, Cancel);

    protected static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);

    /// <summary>Cadastra e confere o 201; devolve o corpo da sessão.</summary>
    protected async Task<JsonElement> RegisterAsync(
        string email = "ana@example.com", string name = "Ana", string password = StrongPassword, HttpClient? client = null)
    {
        var response = await PostAsync("/auth/register", new { name, email, password }, client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    protected static string RefreshTokenOf(JsonElement session) => session.GetProperty("refreshToken").GetString()!;

    protected static string AccessTokenOf(JsonElement session) => session.GetProperty("accessToken").GetString()!;

    /// <summary>Confere que a resposta é um ProblemDetails com o status e o código de erro esperados (RNF-06).</summary>
    protected static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await JsonAsync(response);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        return problem;
    }

    protected static string HashOf(string rawRefreshToken) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawRefreshToken)));

    protected Task<long> CountAsync(string table, string where = "true") =>
        ScalarAsync<long>($"SELECT count(*) FROM {table} WHERE {where}");

    protected async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Cancel))!;
    }

    protected async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancel);
    }

    /// <summary>Uma requisição à API com um access token (o que um cliente logado manda).</summary>
    protected Task<HttpResponseMessage> GetWithTokenAsync(string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return Client.SendAsync(request, Cancel);
    }
}
