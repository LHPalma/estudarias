using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

internal static class SessionTokens
{
    /// <summary>Cada uso de um refresh token abre uma janela nova, então uma sessão ativa não acaba.</summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Cria um refresh token novo na família dada (um login começa uma família; cada renovação continua a mesma). Devolve a
    /// entidade, que guarda só o hash, e o valor cru, que vai uma vez na resposta.
    /// </summary>
    public static (RefreshToken Token, string Raw) IssueRefreshToken(Guid userId, Guid familyId, DateTimeOffset now)
    {
        var (raw, hash) = RefreshTokenSecret.Generate();
        return (RefreshToken.Issue(userId, familyId, hash, now, RefreshTokenLifetime), raw);
    }
}
