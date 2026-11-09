using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

/// <summary>
/// Um PostgreSQL real, compartilhado por todos os testes. Aplica as migrations uma vez em um banco-modelo e entrega a cada teste um
/// banco novo clonado dele, então os testes não se enxergam e ninguém paga a migration de novo.
/// </summary>
/// <remarks>
/// Sobe um contêiner (Testcontainers) por padrão. Onde não há Docker, <c>ESTUDARIAS_TEST_POSTGRES</c> aponta para um servidor
/// existente (uma connection string com permissão de criar bancos).
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ExternalServerVariable = "ESTUDARIAS_TEST_POSTGRES";

    private const string TemplateDatabase = "estudarias_template";

    private PostgreSqlContainer? _container;
    private NpgsqlConnectionStringBuilder _server = new();

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);

        if (string.IsNullOrWhiteSpace(external))
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            external = _container.GetConnectionString();
        }

        _server = new NpgsqlConnectionStringBuilder(external) { Pooling = false };

        await ExecuteAsync($"DROP DATABASE IF EXISTS \"{TemplateDatabase}\" WITH (FORCE)");
        await ExecuteAsync($"CREATE DATABASE \"{TemplateDatabase}\"");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionStringFor(TemplateDatabase)).Options;
        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        await ExecuteAsync($"DROP DATABASE IF EXISTS \"{TemplateDatabase}\" WITH (FORCE)");
    }

    /// <summary>Cria um banco novo, já migrado, e devolve a connection string dele.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"estudarias_test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"");
        return ConnectionStringFor(name);
    }

    public Task DropDatabaseAsync(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        return ExecuteAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_server.ConnectionString) { Database = database }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
