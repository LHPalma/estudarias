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

    public async Task<TrailView?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await StudentReads.StudentIdOfAsync(db, userId, cancellationToken) is not { } id)
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

        var statuses = await StudentReads.StatusesAsync(db, id, cancellationToken);

        // A nota de cada tópico vem das últimas respostas do aluno nele (a janela do TopicScore).
        var answers = await StudentReads.LatestAnswersByTopicAsync(db, id, cancellationToken);

        var now = clock.GetUtcNow();
        var dueReviews = await db.StudentFlashcards
            .CountAsync(card => card.StudentId == id && card.NextReviewAt <= now, cancellationToken);

        var items = TrailOrder
            .Sort(topics, topic => topic.Id, topic => topic.Name, topic => topic.Prerequisites)
            .Select(topic => new TrailTopicView(
                topic.Id,
                topic.Slug,
                topic.Name,
                StudentReads.StatusOf(statuses, topic.Id),
                answers.TryGetValue(topic.Id, out var graded) ? TopicScore.Compute(graded).Display : null,
                topic.Prerequisites))
            .ToList();

        return new TrailView(
            TopicMastery.Threshold,
            items.Count(item => item.Status == TopicStatus.Mastered),
            items.Count,
            dueReviews,
            items);
    }
}
