namespace Estudarias.Application.Trail.Queries;

/// <summary>Superfície de leitura da lição do aluno logado (ADR-002).</summary>
public interface ILessonReads
{
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">
    /// A conta não existe, está desabilitada ou não tem aluno ligado: para o cliente, a sessão deixou de valer.
    /// </exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainNotFoundException">A lição não existe (<c>trail.lesson.notFound</c>).</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainForbiddenException">
    /// O tópico da lição está bloqueado para o aluno (<c>student.topic.locked</c>).
    /// </exception>
    Task<LessonView> FindAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default);
}
