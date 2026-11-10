namespace Estudarias.Application.Common;

/// <summary>
/// Duas requisições alteraram a mesma linha ao mesmo tempo e esta perdeu. Existe para a Application reagir à concorrência (a
/// rotação do refresh token, por exemplo) sem conhecer o tipo de exceção do EF Core.
/// </summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data was changed by another request.", innerException);
