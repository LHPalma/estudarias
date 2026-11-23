using Estudarias.Application.Exercises;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class ExerciseRepository(AppDbContext db) : IExerciseRepository
{
    // As alternativas são uma coleção owned: vêm junto com o exercício, sem Include. O tópico é o da lição, achado na mesma consulta.
    public async Task<ExerciseInTopic?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await (
            from exercise in db.Exercises.AsNoTracking()
            join lesson in db.Lessons on exercise.LessonId equals lesson.Id
            where exercise.Id == id
            select new { Exercise = exercise, lesson.TopicId })
            .FirstOrDefaultAsync(cancellationToken);

        return found is null ? null : new ExerciseInTopic(found.Exercise, found.TopicId);
    }
}
