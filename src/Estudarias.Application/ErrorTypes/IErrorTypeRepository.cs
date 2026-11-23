using Estudarias.Domain.ErrorTypes;

namespace Estudarias.Application.ErrorTypes;

/// <summary>O tipo de erro que o diagnóstico revela. Conteúdo: só a carga o escreve, então aqui não há <c>Add</c>.</summary>
public interface IErrorTypeRepository
{
    /// <summary>O tipo de erro, sem rastreamento, ou nulo quando não existe.</summary>
    Task<ErrorType?> FindAsync(Guid id, CancellationToken cancellationToken);
}
