namespace Estudarias.Domain.StudentFlashcards.Scheduling;

/// <summary>
/// O port de agendamento de revisões. Cada algoritmo de repetição espaçada é um adapter que o implementa, e o estado
/// que ele devolve só é lido pelo próprio adapter.
/// </summary>
public interface IReviewScheduler
{
    /// <summary>Identifica o algoritmo, ex.: <c>sm2</c>. O cartão fica preso ao algoritmo com que nasceu.</summary>
    string Algorithm { get; }

    /// <summary>O estado inicial de um cartão novo e a sua primeira revisão.</summary>
    ReviewOutcome Start(DateTimeOffset now);

    /// <summary>Aplica uma revisão ao estado e agenda a próxima a partir do instante real da revisão.</summary>
    ReviewOutcome Review(string state, ReviewRating rating, DateTimeOffset reviewedAt);
}
