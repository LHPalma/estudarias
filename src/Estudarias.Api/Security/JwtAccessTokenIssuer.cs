using System.Text;
using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Estudarias.Api.Security;

/// <summary>
/// HS256 (simétrico): a mesma chave assina e valida, porque um processo só emite e confere o token, então não há motivo para um par
/// público/privado. O token carrega o mínimo: o id da conta (<c>sub</c>), o e-mail e o papel.
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [AccessClaims.Role] = user.Role.ToString(),
            },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedAccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
