using Estudarias.Application;
using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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

    private readonly List<ServiceProvider> _extraProviders = [];

    /// <summary>O relógio de todos os provedores do teste: o teste o avança para vencer tokens sem esperar.</summary>
    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync();
        _provider = BuildProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        foreach (var extra in _extraProviders)
        {
            await extra.DisposeAsync();
        }

        NpgsqlConnection.ClearAllPools();
        await postgres.DropDatabaseAsync(ConnectionString);
    }

    /// <summary>
    /// Monta a composição da API (<c>AddApplication</c> e <c>AddInfrastructure</c>) sobre o banco deste teste, com o relógio
    /// controlável e um emissor de access token falso. <paramref name="tweak"/> troca a configuração: é como um teste simula uma
    /// rotação de pepper ou a flag do sorteio virada, ligando outro provedor ao mesmo banco.
    /// </summary>
    internal ServiceProvider BuildProvider(Action<Dictionary<string, string?>>? tweak = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = ConnectionString,
            ["PasswordHashing:CurrentPepperVersion"] = "1",
            ["PasswordHashing:Peppers:1"] = Samples.Pepper1,
        };
        tweak?.Invoke(settings);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton<IAccessTokenIssuer, FakeAccessTokenIssuer>();

        var provider = services.BuildServiceProvider();
        if (tweak is not null)
        {
            _extraProviders.Add(provider);
        }

        return provider;
    }

    /// <summary>Um escopo novo, que é o que uma requisição da API teria: contexto, repositórios e unidade de trabalho próprios.</summary>
    internal TestScope NewScope(ServiceProvider? provider = null) => new((provider ?? _provider).CreateScope());

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

    public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose() => scope.Dispose();
}
