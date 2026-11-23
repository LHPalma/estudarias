using Estudarias.Domain.Exercises;

namespace Estudarias.Application.Exercises;

/// <summary>Um exercício com as alternativas e o tópico a que ele pertence (o da lição dele).</summary>
public sealed record ExerciseInTopic(Exercise Exercise, Guid TopicId);

/// <summary>
/// O exercício que o caso de uso de resposta corrige. O conteúdo só é escrito pela carga (comando seed), então aqui não há
/// <c>Add</c>; quem só mostra o exercício (a lição) lê pelo <c>IApplicationDbContext</c> (ADR-002).
/// </summary>
public interface IExerciseRepository
{
    /// <summary>
    /// O exercício com todas as alternativas, inclusive a correta e o tipo de erro de cada errada, sem rastreamento: o caso de uso só
    /// o lê para corrigir. Nulo quando não existe.
    /// </summary>
    Task<ExerciseInTopic?> FindAsync(Guid id, CancellationToken cancellationToken);
}
