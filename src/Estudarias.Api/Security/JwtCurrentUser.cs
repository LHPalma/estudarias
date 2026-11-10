using Estudarias.Application.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Estudarias.Api.Security;

/// <summary>
/// Lê a identidade que o middleware JWT já validou (assinatura, emissor, audiência e vida): não valida nada por conta própria. O id
/// da conta vem da claim <c>sub</c>; sem requisição, sem token válido ou com uma <c>sub</c> que não é um Guid, devolve nulo.
/// </summary>
internal sealed class JwtCurrentUser(IHttpContextAccessor contextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = contextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ? userId : null;
        }
    }
}
