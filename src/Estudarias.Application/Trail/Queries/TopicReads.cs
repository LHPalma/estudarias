using Estudarias.Application.Common;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Mastery;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

public sealed class TopicReads(IApplicationDbContext db) : ITopicReads
{
    private const string PrerequisitesField = "_prerequisites";

    public async Task<TopicView> FindAsync(Guid userId, Guid topicId, CancellationToken cancellationToken = default)
    {
        var studentId = await StudentReads.StudentIdOfAsync(db, userId, cancellationToken)
            ?? throw IdentityErrors.InvalidSession();

        // O grafo inteiro, que é pequeno: o tópico precisa dos nomes dos pré-requisitos e de quem depende dele. Os pré-requisitos não
        // têm propriedade mapeada: leem-se pelo campo (veja IApplicationDbContext.Topics).
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

        var topic = topics.SingleOrDefault(candidate => candidate.Id == topicId) ?? throw TrailErrors.NoSuchTopic();

        var statuses = await StudentReads.StatusesAsync(db, studentId, cancellationToken);
        var (latest, attemptCount) = await new AnswerReads(db).OfTopicAsync(studentId, topicId, cancellationToken);

        // Sem o conteúdo (que pode ser longo): o tópico só lista as lições, e o texto sai na leitura da lição.
        var lessonRows = await db.Lessons
            .Where(lesson => lesson.TopicId == topicId)
            .OrderBy(lesson => lesson.Order)
            .Select(lesson => new { lesson.Id, lesson.Order, lesson.Title })
            .ToListAsync(cancellationToken);

        // O estado de cada lição para o aluno, derivado das tentativas dele (RN-TRL-13).
        var progress = (await StudentReads.LessonProgressAsync(db, studentId, topicId, cancellationToken))
            .ToDictionary(row => row.LessonId, row => row.Progress);
        var lessons = lessonRows
            .Select(lesson => new TopicLessonView(
                lesson.Id,
                lesson.Order,
                lesson.Title,
                progress[lesson.Id].Status,
                progress[lesson.Id].CompletedExercises,
                progress[lesson.Id].TotalExercises))
            .ToList();

        var namesById = topics.ToDictionary(candidate => candidate.Id, candidate => candidate.Name);

        return new TopicView(
            topic.Id,
            topic.Slug,
            topic.Name,
            StudentReads.StatusOf(statuses, topic.Id),
            attemptCount == 0 ? null : TopicScore.Compute(latest).Display,
            attemptCount,
            TopicMastery.Threshold,
            TopicMastery.MinAttempts,
            [.. topic.Prerequisites
                .Where(namesById.ContainsKey)
                .Select(id => new TopicPrerequisiteView(id, namesById[id], StudentReads.StatusOf(statuses, id)))
                .OrderBy(prerequisite => prerequisite.Name, StringComparer.InvariantCultureIgnoreCase)],
            [.. topics
                .Where(candidate => candidate.Prerequisites.Contains(topicId))
                .Select(candidate => new TopicRefView(candidate.Id, candidate.Name))
                .OrderBy(unlocked => unlocked.Name, StringComparer.InvariantCultureIgnoreCase)],
            lessons);
    }
}
