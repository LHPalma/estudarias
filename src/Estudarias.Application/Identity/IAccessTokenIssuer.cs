using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Emite o access token de vida curta que a API aceita como credencial bearer.</summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(User user);
}
