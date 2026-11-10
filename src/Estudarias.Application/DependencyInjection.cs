using Estudarias.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Estudarias.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Os casos de uso recebem o relógio por TimeProvider, então os testes controlam o tempo.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<RegisterStudentHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();

        return services;
    }
}
