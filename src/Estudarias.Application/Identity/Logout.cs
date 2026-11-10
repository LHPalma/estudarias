using Estudarias.Application.Common;

namespace Estudarias.Application.Identity;

public sealed record LogoutCommand(string RefreshToken);

/// <summary>
/// Revoga o refresh token daquele dispositivo. O access token já emitido vale até expirar: ele não guarda estado e não pode ser
/// retomado antes. Um token desconhecido, vencido ou já revogado não é erro de quem chama.
/// </summary>
public sealed class LogoutHandler(IRefreshTokenRepository refreshTokens, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = RefreshTokenSecret.Hash(command.RefreshToken ?? "");

        var existing = await refreshTokens.FindByHashAsync(hash, cancellationToken);
        if (existing is null || !existing.IsActive(now))
        {
            return;
        }

        existing.Revoke(now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Revogado por outra requisição nesse meio-tempo: a sessão acabou de qualquer jeito.
        }
    }
}
