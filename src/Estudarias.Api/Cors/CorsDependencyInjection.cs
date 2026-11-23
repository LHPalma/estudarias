using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Estudarias.Api.Cors;

public static class CorsDependencyInjection
{
    /// <summary>Há tempo de sobra para o navegador guardar a resposta do preflight, sem reconsultar a API a cada chamada.</summary>
    private static readonly TimeSpan PreflightMaxAge = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Em Development qualquer origem é aceita, porque o Expo na web sobe em portas que variam (a porta ocupada muda a do Metro) e
    /// a API local não guarda nada que valha proteger por origem. Nos demais ambientes, só as origens de <c>Cors:AllowedOrigins</c>.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Uma origem mal escrita derruba a subida: melhor do que descobrir, no navegador, um app que nunca consegue chamar a API.
        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .Validate(
                settings => settings.AllowedOrigins.All(CorsSettings.IsValidOrigin),
                "Cors:AllowedOrigins: every origin must be http or https, with host and optional port, and no path or trailing slash.")
            .ValidateOnStart();

        // A política padrão vale para todos os endpoints. Sem credenciais (cookie): o app manda o access token no Authorization, e é
        // por isso que aceitar qualquer origem em Development não expõe sessão alguma.
        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<CorsSettings>>((cors, settings) =>
                cors.AddDefaultPolicy(policy =>
                {
                    if (environment.IsDevelopment())
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        policy.WithOrigins(settings.Value.AllowedOrigins);
                    }

                    policy
                        .WithMethods(HttpMethods.Get, HttpMethods.Post)
                        .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType)
                        .SetPreflightMaxAge(PreflightMaxAge);
                }));

        return services;
    }
}
