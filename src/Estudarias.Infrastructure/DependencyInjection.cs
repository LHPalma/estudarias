using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Infrastructure.Persistence;
using Estudarias.Infrastructure.Persistence.Repositories;
using Estudarias.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings__{ConnectionStringName}.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();

        // Um pepper ausente, curto ou com a versão corrente fora do mapa derruba a subida: melhor do que descobrir no primeiro login.
        services.AddOptions<PasswordHashingOptions>()
            .Bind(configuration.GetSection(PasswordHashingOptions.SectionName))
            .Validate(o => o.Peppers.Count > 0, "PasswordHashing:Peppers is not configured: there is no pepper.")
            .Validate(o => o.Peppers.ContainsKey(o.CurrentPepperVersion), "PasswordHashing:CurrentPepperVersion is not in Peppers.")
            .Validate(
                o => o.Peppers.Values.All(p => p is { Length: >= PasswordHashingOptions.MinimumPepperLength }),
                $"PasswordHashing: every pepper needs at least {PasswordHashingOptions.MinimumPepperLength} characters.")
            .ValidateOnStart();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

        return services;
    }
}
