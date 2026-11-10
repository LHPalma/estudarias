using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Estudarias.Api.FunctionalTests.Support;

/// <summary>
/// Tokens montados à mão, fora do emissor da API, para provar o que o middleware recusa: outro algoritmo, sem assinatura, ou um papel
/// que o emissor nunca emitiria.
/// </summary>
internal static class CraftedTokens
{
    public static string Create(
        string? signingKey = ApiFactory.SigningKey,
        string algorithm = SecurityAlgorithms.HmacSha256,
        string role = "Student",
        Guid? userId = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Estudarias",
            Audience = "Estudarias",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = (userId ?? Guid.CreateVersion7()).ToString(),
                ["email"] = "ana@example.com",
                ["role"] = role,
            },
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = signingKey is null
                ? null
                : new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), algorithm),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
