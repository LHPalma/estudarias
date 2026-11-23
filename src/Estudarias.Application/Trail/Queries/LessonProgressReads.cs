using Estudarias.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

public sealed class LessonProgressReads(IApplicationDbContext db) : ILessonProgressReads
{
    public async Task<LessonAnswers> OfLessonAsync(Guid studentId, Guid lessonId, CancellationToken cancellationToken)
    {
        var exerciseIds = await db.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .Select(exercise => exercise.Id)
            .ToListAsync(cancellationToken);

        var (attempted, correct) = await StudentReads.AttemptedAndCorrectAsync(db, studentId, exerciseIds, cancellationToken);

        return new LessonAnswers(exerciseIds, attempted, correct);
    }
}
