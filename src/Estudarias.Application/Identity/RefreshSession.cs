using Estudarias.Application.Common;
using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

public sealed record RefreshSessionCommand(string RefreshToken);

/// <summary>
/// Troca um refresh token por um par novo. O token recebido é sempre revogado, mesmo que não tivesse expirado. Um token que já foi
/// usado só reaparece se vazou, então a família inteira é revogada.
/// </summary>
public sealed class RefreshSessionHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<AuthPayload> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = RefreshTokenSecret.Hash(command.RefreshToken ?? "");

        var existing = await refreshTokens.FindByHashAsync(hash, cancellationToken)
            ?? throw IdentityErrors.InvalidRefreshToken();

        if (existing.RevokedAt is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, now, cancellationToken);
            throw IdentityErrors.InvalidRefreshToken();
        }

        var user = await users.FindByIdAsync(existing.UserId, cancellationToken);
        if (!existing.IsActive(now) || user is not { IsActive: true })
        {
            throw IdentityErrors.InvalidRefreshToken();
        }

        var (successor, rawRefreshToken) = SessionTokens.IssueRefreshToken(user.Id, existing.FamilyId, now);
        refreshTokens.Add(successor);
        existing.Revoke(now, successor.Id);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Outra requisição usou o mesmo token primeiro (a revogação é o token de concorrência), então esta perdeu.
            throw IdentityErrors.InvalidRefreshToken();
        }

        var access = accessTokens.Issue(user);
        return new AuthPayload(access.Token, access.ExpiresAt, rawRefreshToken, user);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unrevoked = await refreshTokens.ListUnrevokedByFamilyAsync(familyId, cancellationToken);
        if (unrevoked.Count == 0)
        {
            return;
        }

        foreach (var token in unrevoked)
        {
            token.Revoke(now);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Outra requisição revogou parte da família nesse meio-tempo; o resultado é o mesmo.
        }
    }
}
