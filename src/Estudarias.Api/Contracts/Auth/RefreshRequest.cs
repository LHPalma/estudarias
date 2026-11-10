namespace Estudarias.Api.Contracts.Auth;

/// <summary>O refresh token cru, como o app o recebeu na resposta anterior.</summary>
public sealed record RefreshRequest(string? RefreshToken);
