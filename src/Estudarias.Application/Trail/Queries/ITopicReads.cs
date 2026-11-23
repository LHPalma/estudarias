namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// Superfície de leitura do tópico do aluno logado (ADR-002). Diferente das outras leituras, aqui o que falta é erro com código,
/// porque o contrato distingue os casos.
/// </summary>
public interface ITopicReads
{
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">
    /// A conta não existe, está desabilitada ou não tem aluno ligado: para o cliente, a sessão deixou de valer.
    /// </exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainNotFoundException">O tópico não existe (<c>trail.topic.notFound</c>).</exception>
    Task<TopicView> FindAsync(Guid userId, Guid topicId, CancellationToken cancellationToken = default);
}
