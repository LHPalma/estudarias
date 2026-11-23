using Estudarias.Domain.Scoring;

namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// As respostas do aluno num tópico, o que o caso de uso de resposta precisa para recalcular a nota e o domínio. É uma porta de
/// leitura (ADR-002): o caso de uso não toca o contexto, e o teste dele a substitui por um dublê.
/// </summary>
public interface IAnswerReads
{
    /// <summary>
    /// A janela da nota (as respostas mais recentes, da mais recente para a mais antiga) e o total de tentativas do aluno no tópico,
    /// <b>sem</b> a resposta que o caso de uso ainda vai gravar.
    /// </summary>
    Task<(IReadOnlyList<GradedAnswer> Latest, int Total)> OfTopicAsync(Guid studentId, Guid topicId, CancellationToken cancellationToken);
}
