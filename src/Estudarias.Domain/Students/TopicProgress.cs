using Estudarias.Domain.Common;

namespace Estudarias.Domain.Students;

public sealed class TopicProgress : BaseEntity
{
    public Guid TopicId { get; }
    public DateTimeOffset UnlockedAt { get; }
    public DateTimeOffset? MasteredAt { get; private set; }

    public TopicStatus Status => MasteredAt is null ? TopicStatus.Available : TopicStatus.Mastered;

    internal TopicProgress(Guid topicId, DateTimeOffset unlockedAt)
    {
        TopicId = topicId;
        UnlockedAt = unlockedAt;
    }

    internal void MarkMastered(DateTimeOffset now) => MasteredAt ??= now;
}
