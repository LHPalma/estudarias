using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListUnrevokedByFamilyAsync(
        Guid familyId, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

    // Carrega e remove pelo contexto (em vez de um DELETE direto) para a remoção entrar na mesma gravação do caso de uso.
    public async Task RemoveExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        db.RefreshTokens.RemoveRange(expired);
    }

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
