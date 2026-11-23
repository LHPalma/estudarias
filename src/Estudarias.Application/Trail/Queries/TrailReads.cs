using Estudarias.Application.Common;
using Estudarias.Domain.Mastery;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

public sealed class TrailReads(IApplicationDbContext db, TimeProvider clock) : ITrailReads
{
    private const string PrerequisitesField = "_prerequisites";

    public async Task<TrailResponse?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var studentId = await db.Users
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.StudentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentId is not { } id)
        {
            return null;
        }

        // Os pré-requisitos não têm propriedade mapeada: leem-se pelo campo (veja IApplicationDbContext.Topics).
        var topics = await db.Topics
            .Select(topic => new
            {
                topic.Id,
                topic.Slug,
                topic.Name,
                Prerequisites = EF.Property<IEnumerable<TopicPrerequisite>>(topic, PrerequisitesField)
                    .Select(prerequisite => prerequisite.PrerequisiteId)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // O status não é coluna: deriva de mastered_at, e um tópico sem linha de progresso está bloqueado.
        var progress = (await db.Students
                .Where(student => student.Id == id)
                .SelectMany(student => student.Progress)
                .Select(value => new { value.TopicId, value.MasteredAt })
                .ToListAsync(cancellationToken))
            .ToDictionary(value => value.TopicId, value => value.MasteredAt);

        // A nota de cada tópico vem das últimas respostas do aluno nele (a janela do TopicScore): da mais recente para a mais antiga,
        // com o desempate pelo id (v7, que cresce com o tempo) para duas respostas no mesmo instante saírem sempre na mesma ordem.
        // Só as colunas que a nota usa; o tópico de um exercício é o da lição dele.
        var answers = (await (
                from attempt in db.Attempts
                join exercise in db.Exercises on attempt.ExerciseId equals exercise.Id
                join lesson in db.Lessons on exercise.LessonId equals lesson.Id
                where attempt.StudentId == id
                orderby attempt.AnsweredAt descending, attempt.Id descending
                select new
                {
                    lesson.TopicId,
                    exercise.Difficulty,
                    IsCorrect = exercise.Alternatives.Any(alternative => alternative.Id == attempt.AlternativeId && alternative.IsCorrect),
                })
                .ToListAsync(cancellationToken))
            .GroupBy(answer => answer.TopicId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<GradedAnswer>)[.. group
                    .Take(TopicScore.Window)
                    .Select(answer => new GradedAnswer(answer.Difficulty, answer.IsCorrect))]);

        var now = clock.GetUtcNow();
        var dueReviews = await db.StudentFlashcards
            .CountAsync(card => card.StudentId == id && card.NextReviewAt <= now, cancellationToken);

        var items = TrailOrder
            .Sort(topics, topic => topic.Id, topic => topic.Name, topic => topic.Prerequisites)
            .Select(topic => new TrailTopicResponse(
                topic.Id,
                topic.Slug,
                topic.Name,
                StatusOf(progress, topic.Id),
                answers.TryGetValue(topic.Id, out var graded) ? TopicScore.Compute(graded).Display : null,
                topic.Prerequisites))
            .ToList();

        return new TrailResponse(
            TopicMastery.Threshold,
            items.Count(item => item.Status == TopicStatus.Mastered),
            items.Count,
            dueReviews,
            items);
    }

    private static TopicStatus StatusOf(Dictionary<Guid, DateTimeOffset?> progress, Guid topicId) =>
        !progress.TryGetValue(topicId, out var masteredAt)
            ? TopicStatus.Locked
            : masteredAt is null ? TopicStatus.Available : TopicStatus.Mastered;
}
