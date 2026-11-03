using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Students;

public sealed class Student : BaseEntity, IAggregateRoot
{
    public const int NameMaxLength = 100;

    private readonly List<TopicProgress> _progress = [];

    public string Name { get; }
    public ExperimentalGroup Group { get; }
    public IReadOnlyCollection<TopicProgress> Progress => _progress;

    private Student(string name, ExperimentalGroup group)
    {
        Name = name;
        Group = group;
    }

    public static Student Create(string name, ExperimentalGroup group)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(StudentErrors.NameRequired, "Student name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainValidationException(
                StudentErrors.NameTooLong, $"Student name must have at most {NameMaxLength} characters.");
        }

        if (!Enum.IsDefined(group))
        {
            throw new DomainValidationException(StudentErrors.GroupInvalid, "Experimental group is not valid.");
        }

        return new Student(trimmed, group);
    }

    public TopicStatus StatusOf(Guid topicId) => Find(topicId)?.Status ?? TopicStatus.Locked;

    public void Unlock(Guid topicId, TimeProvider clock)
    {
        if (Find(topicId) is not null)
        {
            return;
        }

        _progress.Add(new TopicProgress(topicId, clock.GetUtcNow()));
    }

    public void MarkMastered(Guid topicId, TimeProvider clock)
    {
        var progress = Find(topicId)
            ?? throw new DomainValidationException(
                StudentErrors.TopicLocked, "A locked topic cannot be mastered.");

        progress.MarkMastered(clock.GetUtcNow());
    }

    private TopicProgress? Find(Guid topicId) => _progress.FirstOrDefault(p => p.TopicId == topicId);
}
