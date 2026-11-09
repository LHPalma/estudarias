namespace Estudarias.Domain.Common.Exceptions;

/// <summary>
/// O pedido está bem formado, mas conflita com o estado atual (e-mail já cadastrado, token já revogado).
/// </summary>
public sealed class DomainConflictException(string code, string message)
    : BaseDomainException(code, message);
