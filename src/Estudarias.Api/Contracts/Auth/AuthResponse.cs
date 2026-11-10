using Estudarias.Application.Identity;

namespace Estudarias.Api.Contracts.Auth;

/// <summary>O usuário da sessão. A conta não guarda o nome: ele está no perfil de estudo (o aluno), ligado por <c>StudentId</c>.</summary>
public sealed record UserResponse(Guid Id, string Email, string Role, Guid? StudentId);

/// <summary>
/// O que um cadastro, um login ou uma renovação devolve. <c>AccessTokenExpiresAt</c> vai junto para o app renovar a sessão sem
/// decodificar o JWT. O refresh token cru só existe nesta resposta: o servidor guarda só o hash.
/// </summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserResponse User)
{
    public static AuthResponse From(AuthPayload payload) =>
        new(
            payload.AccessToken,
            payload.AccessTokenExpiresAt,
            payload.RefreshToken,
            new UserResponse(payload.User.Id, payload.User.Email, payload.User.Role.ToString(), payload.User.StudentId));
}
