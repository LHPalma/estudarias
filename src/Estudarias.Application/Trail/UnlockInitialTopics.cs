using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Application.Topics;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Mastery;

namespace Estudarias.Application.Trail;

/// <summary>
/// Garante que o aluno da conta tem os tópicos sem pré-requisito liberados (<see cref="TopicMastery.UnlockInitial"/>). O cadastro já
/// libera; este caso de uso cobre quem se cadastrou antes de existir conteúdo e o conteúdo que a carga acrescentou depois. É escrita,
/// então passa por aqui antes da leitura da trilha (ADR-002). É idempotente: sem nada a liberar, não grava.
/// </summary>
public sealed class UnlockInitialTopicsHandler(
    IUserRepository users,
    IStudentRepository students,
    ITopicRepository topics,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">
    /// A conta não existe, está desabilitada ou não tem aluno ligado: para o cliente, a sessão deixou de valer.
    /// </exception>
    public async Task Handle(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is not { IsActive: true, StudentId: { } studentId })
        {
            throw IdentityErrors.InvalidSession();
        }

        var student = await students.FindAsync(studentId, cancellationToken)
            ?? throw IdentityErrors.InvalidSession();

        var unlocked = TopicMastery.UnlockInitial(student, await topics.ListAsync(cancellationToken), clock);
        if (unlocked.Count == 0)
        {
            return;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Duas aberturas da trilha ao mesmo tempo liberam o mesmo tópico, e só o índice único (aluno e tópico) as separa: a
            // perdedora não tem o que fazer, porque o tópico já está liberado pela vencedora.
        }
    }
}
