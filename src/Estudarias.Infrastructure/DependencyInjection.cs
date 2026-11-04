using Estudarias.Application.Students;
using Estudarias.Application.Topics;
using Estudarias.Infrastructure.Persistence.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra as implementações das abstrações da Application.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTopicRepository>();
        services.AddSingleton<ITopicRepository>(provider => provider.GetRequiredService<InMemoryTopicRepository>());
        services.AddSingleton<InMemoryStudentRepository>();
        services.AddSingleton<IStudentRepository>(provider => provider.GetRequiredService<InMemoryStudentRepository>());

        return services;
    }
}
