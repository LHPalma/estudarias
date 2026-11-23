namespace Estudarias.Application.Trail.Queries;

/// <summary>Superfície de leitura do histórico de respostas do aluno logado num tópico (ADR-002).</summary>
public interface ITopicAttemptsReads
{
    /// <summary>
    /// As respostas do aluno no tópico que entram na nota (a janela das mais recentes), da mais recente para a mais antiga, no mesmo
    /// critério de ordenação do cálculo (RN-TRL-17). Um tópico liberado sem respostas devolve a lista vazia.
    /// </summary>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">
    /// A conta não existe, está desabilitada ou não tem aluno ligado: para o cliente, a sessão deixou de valer.
    /// </exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainNotFoundException">O tópico não existe (<c>trail.topic.notFound</c>).</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainForbiddenException">O tópico está bloqueado para o aluno (<c>student.topic.locked</c>).</exception>
    Task<IReadOnlyList<TopicAttemptView>> FindAsync(Guid userId, Guid topicId, CancellationToken cancellationToken = default);
}
