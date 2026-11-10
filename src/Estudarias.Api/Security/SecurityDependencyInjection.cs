using System.Text;
using Estudarias.Application.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Estudarias.Api.Security;

public static class SecurityDependencyInjection
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        // Uma chave ausente ou curta derruba a subida: melhor do que descobrir no primeiro login, ou pior, assinar com uma chave fraca.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinimumSigningKeyLength,
                $"Jwt:SigningKey needs at least {JwtOptions.MinimumSigningKeyLength} characters.")
            .Validate(o => o.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be positive.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience must not be blank.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<ICurrentUser, JwtCurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Os parâmetros de validação vêm das mesmas opções que assinam o token, resolvidos quando o esquema é usado pela primeira
        // vez: então a configuração de cada ambiente (e a dos testes) é a que vale.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Mantém os nomes das claims como estão no token: `sub` continua `sub`.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    // Só o algoritmo que o emissor usa: um token assinado com outro (ou sem assinatura) é recusado.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = AccessClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // Login é exigido em toda a API por padrão: um endpoint novo já nasce protegido, e quem precisa ser aberto (a sessão, o
        // health) declara [AllowAnonymous] ou AllowAnonymous() de propósito.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
