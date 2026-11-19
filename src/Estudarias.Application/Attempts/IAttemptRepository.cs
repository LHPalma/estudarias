using Estudarias.Domain.Attempts;

namespace Estudarias.Application.Attempts;

/// <summary>
/// A escrita das tentativas. Uma tentativa nunca muda depois de gravada, então só há <see cref="Add"/>; o que se calcula delas (a
/// nota, o histórico, o estado da lição) é leitura, pelo <c>IApplicationDbContext</c> (ADR-002).
/// </summary>
public interface IAttemptRepository
{
    /// <summary>Marca a tentativa para ser gravada; só vale depois de <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(Attempt attempt);
}
