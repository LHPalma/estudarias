using Estudarias.Application.Common;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

public sealed class LessonReads(IApplicationDbContext db) : ILessonReads
{
    public async Task<LessonView> FindAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default)
    {
        var studentId = await StudentReads.StudentIdOfAsync(db, userId, cancellationToken)
            ?? throw IdentityErrors.InvalidSession();

        var lesson = await db.Lessons
            .Where(candidate => candidate.Id == lessonId)
            .Select(candidate => new { candidate.Id, candidate.TopicId, candidate.Order, candidate.Title, candidate.Content })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw TrailErrors.NoSuchLesson();

        // O tópico bloqueado abre, mas a lição dele não: quem não dominou os pré-requisitos ainda não pratica (RN-TRL-08). A lição
        // que não existe é 404, e só a que existe de um tópico bloqueado é 403.
        var statuses = await StudentReads.StatusesAsync(db, studentId, cancellationToken);
        if (StudentReads.StatusOf(statuses, lesson.TopicId) == TopicStatus.Locked)
        {
            throw StudentErrors.LockedTopic();
        }

        // Só id e texto das alternativas: is_correct e error_type_id não são lidos, então não há como saírem por serialização
        // (RN-TRL-11). Os exercícios e as alternativas vêm na ordem do conteúdo, que é gravada (#104); o id desempata posições
        // repetidas, que sobram quando a recarga do conteúdo deixa de listar um exercício.
        var exercises = await db.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.Position)
            .ThenBy(exercise => exercise.Id)
            .Select(exercise => new
            {
                exercise.Id,
                exercise.Statement,
                exercise.Difficulty,
                Alternatives = exercise.Alternatives
                    .OrderBy(alternative => alternative.Position)
                    .Select(alternative => new LessonAlternativeView(alternative.Id, alternative.Text))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return new LessonView(
            lesson.Id,
            lesson.TopicId,
            lesson.Order,
            lesson.Title,
            lesson.Content,
            [.. exercises.Select(exercise => new LessonExerciseView(exercise.Id, exercise.Statement, exercise.Difficulty, exercise.Alternatives))]);
    }
}
