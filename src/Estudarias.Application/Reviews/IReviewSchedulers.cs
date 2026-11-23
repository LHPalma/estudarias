namespace Estudarias.Application.Reviews;

/// <summary>
/// Escolhe o agendador pelo algoritmo gravado no cartão: o cartão fica preso ao algoritmo com que nasceu (ADR-003), e os cartões novos
/// usam o algoritmo ativo, o <c>IReviewScheduler</c> registrado no composition root.
/// </summary>
public interface IReviewSchedulers
{
    /// <exception cref="InvalidOperationException">
    /// Nenhum agendador está registrado para o algoritmo: é erro de configuração (RN-REV-13), e não erro do aluno.
    /// </exception>
    Estudarias.Domain.StudentFlashcards.Scheduling.IReviewScheduler For(string algorithm);
}
