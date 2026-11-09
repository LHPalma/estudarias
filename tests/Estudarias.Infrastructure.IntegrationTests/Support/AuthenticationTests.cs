using System.Security.Cryptography;
using System.Text;
using Estudarias.Application.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

/// <summary>
/// Base dos testes de fluxo: chama os casos de uso como a API chamaria, cada chamada num escopo próprio (uma requisição), sobre um
/// PostgreSQL real. Os testes conferem o resultado olhando o banco direto, não só o que o caso de uso devolve.
/// </summary>
public abstract class AuthenticationTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    protected const string StrongPassword = "Senha-valida-1!";

    internal async Task<AuthPayload> RegisterAsync(
        string email = "ana@example.com",
        string name = "Ana",
        string password = StrongPassword,
        ServiceProvider? provider = null)
    {
        using var scope = NewScope(provider);
        return await scope.Get<RegisterStudentHandler>()
            .Handle(new RegisterStudentCommand(name, email, password), CancellationToken.None);
    }

    internal async Task<AuthPayload> LoginAsync(
        string email = "ana@example.com",
        string password = StrongPassword,
        ServiceProvider? provider = null)
    {
        using var scope = NewScope(provider);
        return await scope.Get<LoginHandler>().Handle(new LoginCommand(email, password), CancellationToken.None);
    }

    internal async Task<AuthPayload> RefreshAsync(string refreshToken, ServiceProvider? provider = null)
    {
        using var scope = NewScope(provider);
        return await scope.Get<RefreshSessionHandler>()
            .Handle(new RefreshSessionCommand(refreshToken), CancellationToken.None);
    }

    internal async Task LogoutAsync(string refreshToken)
    {
        using var scope = NewScope();
        await scope.Get<LogoutHandler>().Handle(new LogoutCommand(refreshToken), CancellationToken.None);
    }

    /// <summary>O hash que o banco guarda de um refresh token cru: o SHA-256 em hexadecimal minúsculo.</summary>
    protected static string HashOf(string rawRefreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken)));

    internal Task<long> CountAsync(string table, string where = "true") =>
        ScalarAsync<long>($"SELECT count(*) FROM {table} WHERE {where}");

    internal Task<Guid> FamilyOfAsync(string rawRefreshToken) =>
        ScalarAsync<Guid>($"SELECT family_id FROM refresh_token WHERE token_hash = '{HashOf(rawRefreshToken)}'");

    /// <summary>Roda a ação e devolve o resultado ou a exceção, para os testes de corrida contarem quem venceu e quem perdeu.</summary>
    internal static async Task<Outcome<T>> AttemptAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return new Outcome<T>(await action(), null);
        }
        catch (Exception exception)
        {
            return new Outcome<T>(default, exception);
        }
    }
}

internal sealed record Outcome<T>(T? Value, Exception? Error)
{
    public bool Succeeded => Error is null;
}
