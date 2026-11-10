namespace Estudarias.Application.Common;

/// <summary>
/// Uma gravação quebrou um índice único. Existe para a Application reagir à corrida entre duas requisições (dois cadastros com o
/// mesmo e-mail ao mesmo tempo) sem conhecer o tipo de exceção do banco.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"A unique constraint was violated ({constraintName ?? "unknown"}).", innerException)
{
    /// <summary>O nome do índice ou da constraint violada, quando o banco o informa.</summary>
    public string? ConstraintName { get; } = constraintName;
}
