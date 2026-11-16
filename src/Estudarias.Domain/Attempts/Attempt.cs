using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Attempts;

public sealed class Attempt : BaseEntity, IAggregateRoot
{
    public Guid StudentId { get; }
    public Guid ExerciseId { get; }
    public Guid AlternativeId { get; }
    public int? ResponseSeconds { get; }
    public DateTimeOffset AnsweredAt { get; }

    private Attempt(
        Guid studentId, Guid exerciseId, Guid alternativeId, int? responseSeconds, DateTimeOffset answeredAt)
    {
        StudentId = studentId;
        ExerciseId = exerciseId;
        AlternativeId = alternativeId;
        ResponseSeconds = responseSeconds;
        AnsweredAt = answeredAt;
    }

    public static Attempt Create(
        Guid studentId, Guid exerciseId, Guid alternativeId, int? responseSeconds, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (studentId == Guid.Empty)
        {
            throw new DomainValidationException(AttemptErrors.StudentRequired, "Attempt student is required.");
        }

        if (exerciseId == Guid.Empty)
        {
            throw new DomainValidationException(AttemptErrors.ExerciseRequired, "Attempt exercise is required.");
        }

        if (alternativeId == Guid.Empty)
        {
            throw new DomainValidationException(AttemptErrors.AlternativeRequired, "Attempt alternative is required.");
        }

        if (responseSeconds < 0)
        {
            throw new DomainValidationException(
                AttemptErrors.ResponseTimeInvalid, "Attempt response time cannot be negative.");
        }

        return new Attempt(studentId, exerciseId, alternativeId, responseSeconds, clock.GetUtcNow());
    }
}
