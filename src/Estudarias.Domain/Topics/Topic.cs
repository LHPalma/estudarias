using System.Text.RegularExpressions;
using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Topics;

public sealed partial class Topic : BaseEntity, IAggregateRoot
{
    public const int NameMaxLength = 100;
    public const int SlugMaxLength = 100;

    private readonly List<Guid> _prerequisites = [];

    public string Name { get; }
    public string Slug { get; }
    public IReadOnlyCollection<Guid> Prerequisites => _prerequisites;

    private Topic(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex SlugFormat();

    public static Topic Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(TopicErrors.NameRequired, "Topic name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainValidationException(
                TopicErrors.NameTooLong, $"Topic name must have at most {NameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainValidationException(TopicErrors.SlugRequired, "Topic slug is required.");
        }

        if (slug.Length > SlugMaxLength || !SlugFormat().IsMatch(slug))
        {
            throw new DomainValidationException(
                TopicErrors.SlugInvalid,
                $"Topic slug must have lowercase letters and digits separated by single hyphens, with at most {SlugMaxLength} characters.");
        }

        return new Topic(trimmed, slug);
    }

    public void AddPrerequisite(Guid requisiteId)
    {
        if (requisiteId == Id)
        {
            throw new DomainValidationException(
                TopicErrors.PrerequisiteSelf, "A topic cannot be a prerequisite of itself.");
        }

        if (_prerequisites.Contains(requisiteId))
        {
            throw new DomainValidationException(
                TopicErrors.PrerequisiteDuplicated, "The prerequisite was already added to this topic.");
        }

        _prerequisites.Add(requisiteId);
    }
}
