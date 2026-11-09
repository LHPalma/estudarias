using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Estudarias.Infrastructure.Persistence;

/// <summary>
/// Usada só pelas ferramentas (<c>dotnet ef migrations add</c>), que não passam pela composição da API. Gerar uma migration
/// não abre conexão, então o padrão de desenvolvimento basta; <c>ESTUDARIAS_CONNECTION_STRING</c> aponta para outro banco
/// quando é preciso aplicá-la com <c>dotnet ef database update</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=estudarias;Username=estudarias;Password=estudarias";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ESTUDARIAS_CONNECTION_STRING") ?? DevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        return new AppDbContext(options);
    }
}
