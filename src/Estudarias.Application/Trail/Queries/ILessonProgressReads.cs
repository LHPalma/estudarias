namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// O que o caso de uso de resposta precisa das tentativas do aluno numa lição para calcular o estado dela: os exercícios da lição, os que
/// o aluno já tentou e os que já acertou, <b>sem</b> a resposta que o caso de uso ainda vai gravar. É uma porta de leitura (ADR-002).
/// </summary>
public interface ILessonProgressReads
{
    Task<LessonAnswers> OfLessonAsync(Guid studentId, Guid lessonId, CancellationToken cancellationToken);
}

/// <summary>Os exercícios de uma lição e, deles, os que o aluno tentou e os que acertou pelo menos uma vez.</summary>
public sealed record LessonAnswers(
    IReadOnlyCollection<Guid> ExerciseIds, IReadOnlyCollection<Guid> Attempted, IReadOnlyCollection<Guid> Correct);
