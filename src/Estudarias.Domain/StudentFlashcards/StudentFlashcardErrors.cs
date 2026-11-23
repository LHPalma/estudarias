using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.StudentFlashcards;

public static class StudentFlashcardErrors
{
    public const string StudentRequired = "studentFlashcard.student.required";
    public const string FlashcardRequired = "studentFlashcard.flashcard.required";
    public const string OriginAttemptInvalid = "studentFlashcard.originAttempt.invalid";
    public const string RatingInvalid = "studentFlashcard.rating.invalid";
    public const string SchedulerMismatch = "studentFlashcard.scheduler.mismatch";

    /// <summary>A nota da revisão que não é uma das quatro (ou que não veio): nada é gravado.</summary>
    public static DomainValidationException InvalidRating() =>
        new(RatingInvalid, "Review rating is not valid.");
}
