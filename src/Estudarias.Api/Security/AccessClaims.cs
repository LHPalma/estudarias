namespace Estudarias.Api.Security;

internal static class AccessClaims
{
    /// <summary>O papel da conta (<c>Student</c>). Não é a claim <c>ClaimTypes.Role</c> da Microsoft: o token guarda o nome curto.</summary>
    public const string Role = "role";
}
