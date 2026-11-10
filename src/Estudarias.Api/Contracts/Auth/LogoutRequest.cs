namespace Estudarias.Api.Contracts.Auth;

/// <summary>O refresh token do dispositivo que está saindo.</summary>
public sealed record LogoutRequest(string? RefreshToken);
