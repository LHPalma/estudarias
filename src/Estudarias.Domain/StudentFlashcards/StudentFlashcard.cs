using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.StudentFlashcards.Scheduling;

namespace Estudarias.Domain.StudentFlashcards;

/// <summary>
/// O estado de revisão de um flashcard para um aluno. O cálculo é do <see cref="IReviewScheduler"/>, e o estado de
/// agendamento é opaco: o agregado o guarda, mas só o adapter do algoritmo o interpreta.
/// </summary>
public sealed class StudentFlashcard : BaseEntity, IAggregateRoot
{
    public Guid StudentId { get; }
    public Guid FlashcardId { get; }
    public Guid? OriginAttemptId { get; }
    public string Algorithm { get; }
    public string SchedulingState { get; private set; }
    public DateTimeOffset NextReviewAt { get; private set; }

    private StudentFlashcard(
        Guid studentId,
        Guid flashcardId,
        Guid? originAttemptId,
        string algorithm,
        string schedulingState,
        DateTimeOffset nextReviewAt)
    {
        StudentId = studentId;
        FlashcardId = flashcardId;
        OriginAttemptId = originAttemptId;
        Algorithm = algorithm;
        SchedulingState = schedulingState;
        NextReviewAt = nextReviewAt;
    }

    public static StudentFlashcard Create(
        Guid studentId, Guid flashcardId, Guid? originAttemptId, IReviewScheduler scheduler, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);

        if (studentId == Guid.Empty)
        {
            throw new DomainValidationException(
                StudentFlashcardErrors.StudentRequired, "Student flashcard student is required.");
        }

        if (flashcardId == Guid.Empty)
        {
            throw new DomainValidationException(
                StudentFlashcardErrors.FlashcardRequired, "Student flashcard flashcard is required.");
        }

        if (originAttemptId == Guid.Empty)
        {
            throw new DomainValidationException(
                StudentFlashcardErrors.OriginAttemptInvalid, "Student flashcard origin attempt cannot be empty.");
        }

        var outcome = scheduler.Start(clock.GetUtcNow());

        return new StudentFlashcard(
            studentId, flashcardId, originAttemptId, scheduler.Algorithm, outcome.State, outcome.NextReviewAt);
    }

    /// <summary>Se a revisão já venceu: a próxima revisão é igual ou anterior ao instante atual.</summary>
    public bool IsDue(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return NextReviewAt <= clock.GetUtcNow();
    }
}
