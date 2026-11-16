namespace Estudarias.Domain.StudentFlashcards.Scheduling;

/// <summary>O resultado de um agendamento: o estado opaco, que só o adapter interpreta, e a próxima revisão.</summary>
public sealed record ReviewOutcome(string State, DateTimeOffset NextReviewAt);
