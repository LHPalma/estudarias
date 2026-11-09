using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Infrastructure.IntegrationTests;

public class DependencyInjectionTests
{
    private static IConfiguration Configuration(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(connectionString is null
                ? []
                : new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();

    [Fact(DisplayName = "AddInfrastructure registra o contexto, os repositórios e a unidade de trabalho")]
    public void AddInfrastructure_RegistersThePersistenceServices()
    {
        // Arrange
        using var provider = new ServiceCollection()
            .AddInfrastructure(Configuration("Host=localhost;Database=x"))
            .BuildServiceProvider(validateScopes: true);

        // Act
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        // Assert
        Assert.NotNull(services.GetRequiredService<AppDbContext>());
        Assert.NotNull(services.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(services.GetRequiredService<IUserRepository>());
        Assert.NotNull(services.GetRequiredService<IRefreshTokenRepository>());
        Assert.NotNull(services.GetRequiredService<IStudentRepository>());
    }

    [Fact(DisplayName = "Dentro de um escopo, o repositório e a unidade de trabalho usam o mesmo contexto")]
    public void Services_InTheSameScope_ShareTheDbContext()
    {
        // Arrange
        using var provider = new ServiceCollection()
            .AddInfrastructure(Configuration("Host=localhost;Database=x"))
            .BuildServiceProvider();

        // Act
        using var one = provider.CreateScope();
        using var two = provider.CreateScope();

        // Assert
        Assert.Same(
            one.ServiceProvider.GetRequiredService<AppDbContext>(),
            one.ServiceProvider.GetRequiredService<AppDbContext>());
        Assert.NotSame(
            one.ServiceProvider.GetRequiredService<AppDbContext>(),
            two.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact(DisplayName = "Sem a connection string, a subida falha dizendo qual variável configurar")]
    public void AddInfrastructure_WithoutConnectionString_Throws()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(Configuration(null)));
        Assert.Contains("ConnectionStrings__Default", exception.Message);
    }
}
