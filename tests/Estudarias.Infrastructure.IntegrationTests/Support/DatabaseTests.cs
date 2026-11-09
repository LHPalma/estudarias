using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

/// <summary>
/// Dá a cada teste um banco só dele, ligado pela mesma composição que a API usa (<c>AddInfrastructure</c>), então os testes
/// passam pelos repositórios e pela unidade de trabalho de verdade.
/// </summary>
public abstract class DatabaseTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    private string ConnectionString { get; set; } = "";

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = ConnectionString,
            })
            .Build();

        _provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await postgres.DropDatabaseAsync(ConnectionString);
    }

    /// <summary>Um escopo novo, que é o que uma requisição da API teria: contexto, repositórios e unidade de trabalho próprios.</summary>
    internal TestScope NewScope() => new(_provider.CreateScope());

    /// <summary>Grava um aluno e a conta dele e devolve a conta.</summary>
    internal async Task<User> SeedUserAsync(string email = "ana@example.com")
    {
        using var scope = NewScope();
        var student = Samples.Student();
        var user = Samples.User(student, email);

        scope.Students.Add(student);
        scope.Users.Add(user);
        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        return user;
    }

    /// <summary>Lê uma coluna direto do banco, para provar o que foi gravado sem passar pelo mapeamento.</summary>
    internal async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    internal async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class TestScope(IServiceScope scope) : IDisposable
{
    public IUserRepository Users => scope.ServiceProvider.GetRequiredService<IUserRepository>();

    public IRefreshTokenRepository RefreshTokens => scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

    public IStudentRepository Students => scope.ServiceProvider.GetRequiredService<IStudentRepository>();

    public IUnitOfWork UnitOfWork => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    public void Dispose() => scope.Dispose();
}
