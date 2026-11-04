using Estudarias.Domain.Students;

namespace Estudarias.Application.Topics.GetTopic;

/// <summary>O tópico como o aluno atual o vê.</summary>
public sealed record TopicDetails(Guid Id, string Name, string Slug, TopicStatus Status)
{
    public bool IsUnlocked => Status != TopicStatus.Locked;
}
