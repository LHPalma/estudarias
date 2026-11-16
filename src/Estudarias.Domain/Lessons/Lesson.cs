using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Lessons;

public sealed class Lesson : BaseEntity, IAggregateRoot
{
    public const int TitleMaxLength = 150;

    public Guid TopicId { get; }
    public int Order { get; }
    public string Title { get; }
    public string Content { get; }

    private Lesson(Guid topicId, int order, string title, string content)
    {
        TopicId = topicId;
        Order = order;
        Title = title;
        Content = content;
    }

    public static Lesson Create(Guid topicId, int order, string title, string content)
    {
        if (topicId == Guid.Empty)
        {
            throw new DomainValidationException(LessonErrors.TopicRequired, "Lesson topic is required.");
        }

        if (order < 1)
        {
            throw new DomainValidationException(LessonErrors.OrderInvalid, "Lesson order must start at 1.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException(LessonErrors.TitleRequired, "Lesson title is required.");
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > TitleMaxLength)
        {
            throw new DomainValidationException(
                LessonErrors.TitleTooLong, $"Lesson title must have at most {TitleMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DomainValidationException(LessonErrors.ContentRequired, "Lesson content is required.");
        }

        return new Lesson(topicId, order, trimmedTitle, content);
    }
}
