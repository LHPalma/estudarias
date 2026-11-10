using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

public interface IRefreshTokenRepository
{
    /// <summary>Procura pelo hash SHA-256 do token (o valor cru nunca é guardado); nulo quando não existe.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Os tokens ainda não revogados de uma família, para revogá-la inteira quando um token usado reaparece.</summary>
    Task<IReadOnlyList<RefreshToken>> ListUnrevokedByFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    /// <summary>
    /// Marca para remoção os tokens do usuário que já venceram, para a tabela não crescer sem fim; só vale depois de
    /// <c>IUnitOfWork.SaveChangesAsync</c>, junto com o resto do caso de uso.
    /// </summary>
    Task RemoveExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Marca o token para ser gravado; só vale depois de <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(RefreshToken token);
}
