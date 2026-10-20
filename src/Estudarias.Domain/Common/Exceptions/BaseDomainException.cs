namespace Estudarias.Domain.Common.Exceptions;

public abstract class BaseDomainException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}
