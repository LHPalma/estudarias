using Estudarias.Domain.Topics;

namespace Estudarias.Application.Topics;

/// <summary>
/// Os tópicos para as regras do Domain que olham o grafo de pré-requisitos (a liberação inicial, o desbloqueio dos dependentes). O
/// conteúdo só é escrito pela carga (comando seed), então aqui não há <c>Add</c>. Quem só mostra o tópico lê pelo
/// <c>IApplicationDbContext</c> (ADR-002).
/// </summary>
public interface ITopicRepository
{
    /// <summary>Todos os tópicos, cada um com os ids dos pré-requisitos, sem rastreamento: servem de entrada para o Domain.</summary>
    Task<IReadOnlyList<Topic>> ListAsync(CancellationToken cancellationToken);
}
