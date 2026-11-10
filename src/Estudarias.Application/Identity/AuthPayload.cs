using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

/// <summary>
/// O que um cadastro, um login ou uma renovação devolve: os dois tokens, quando o access token deixa de valer (para o app renovar sem
/// decodificar o JWT) e o usuário.
/// </summary>
public sealed record AuthPayload(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, User User);
