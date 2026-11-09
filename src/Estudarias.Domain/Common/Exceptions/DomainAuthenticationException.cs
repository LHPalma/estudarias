namespace Estudarias.Domain.Common.Exceptions;

/// <summary>
/// As credenciais ou o refresh token não foram aceitos. Vaga de propósito: a mesma falha para e-mail desconhecido,
/// senha errada e usuário desabilitado, para a resposta não revelar qual parte estava errada.
/// </summary>
public sealed class DomainAuthenticationException(string code, string message)
    : BaseDomainException(code, message);
