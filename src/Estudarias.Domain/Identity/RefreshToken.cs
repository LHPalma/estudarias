using Estudarias.Domain.Common;

namespace Estudarias.Domain.Identity;

/// <summary>
/// Uma credencial de vida longa que troca um access token vencido por um novo sem pedir a senha de novo. Só o hash é
/// guardado, nunca o token. Todo token é de uso único: usá-lo o revoga e emite um sucessor na mesma
/// <see cref="FamilyId"/> (um login, uma família). Um token revogado que aparece de novo indica vazamento, então a família
/// inteira é revogada.
/// </summary>
public sealed class RefreshToken : BaseEntity, IAggregateRoot
{
    private RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; }

    /// <summary>Todos os tokens que descendem de um login.</summary>
    public Guid FamilyId { get; }

    public string TokenHash { get; }

    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Também é o token de concorrência: de dois usos simultâneos do mesmo token, só um o revoga.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>O token que substituiu este quando foi rotacionado; nulo quando foi revogado sozinho.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Issue(Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(userId, familyId, tokenHash, now + lifetime);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            throw IdentityErrors.RefreshTokenRevoked();
        }

        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
