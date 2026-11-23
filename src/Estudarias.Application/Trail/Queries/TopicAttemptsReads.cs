using Estudarias.Application.Common;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

public sealed class TopicAttemptsReads(IApplicationDbContext db) : ITopicAttemptsReads
{
    public async Task<IReadOnlyList<TopicAttemptView>> FindAsync(
        Guid userId, Guid topicId, CancellationToken cancellationToken = default)
    {
        var studentId = await StudentReads.StudentIdOfAsync(db, userId, cancellationToken)
            ?? throw IdentityErrors.InvalidSession();

        // O tópico que não existe é 404, e só o que existe e está bloqueado é 403 (como na lição).
        if (!await db.Topics.AnyAsync(topic => topic.Id == topicId, cancellationToken))
        {
            throw TrailErrors.NoSuchTopic();
        }

        var statuses = await StudentReads.StatusesAsync(db, studentId, cancellationToken);
        if (StudentReads.StatusOf(statuses, topicId) == TopicStatus.Locked)
        {
            throw StudentErrors.LockedTopic();
        }

        // A janela da nota, pela mesma ordenação que o cálculo usa (StudentReads.OrderedSources). Só o enunciado e a correção: a
        // alternativa escolhida e a correta não saem daqui, porque o histórico explica a nota, e não revisa o exercício.
        return await StudentReads.OrderedSources(db, studentId, topicId)
            .Take(TopicScore.Window)
            .Select(source => new TopicAttemptView(
                source.Attempt.Id,
                source.Exercise.Id,
                source.Exercise.Statement,
                source.Exercise.Difficulty,
                source.Exercise.Alternatives.Any(
                    alternative => alternative.Id == source.Attempt.AlternativeId && alternative.IsCorrect),
                source.Attempt.AnsweredAt))
            .ToListAsync(cancellationToken);
    }
}
