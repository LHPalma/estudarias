namespace Estudarias.Application.Reviews.Queries;

/// <summary>Superfície de leitura das revisões do dia do aluno logado (ADR-002).</summary>
public interface IReviewReads
{
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">
    /// A conta não existe, está desabilitada ou não tem aluno ligado: para o cliente, a sessão deixou de valer.
    /// </exception>
    Task<DueReviewsView> DueAsync(Guid userId, CancellationToken cancellationToken = default);
}
