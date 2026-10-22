namespace Estudarias.Domain.Common.Exceptions;

public sealed class DomainValidationException(string code, string message)
    : BaseDomainException(code, message);
