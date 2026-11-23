using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Lessons;

public sealed class Lesson : BaseEntity, IAggregateRoot
{
    public const int TitleMaxLength = 150;

    public Guid TopicId { get; }
    public int Order { get; }
    public string Title { get; private set; }
    public string Content { get; private set; }

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

        var (validTitle, validContent) = ValidContent(title, content);

        return new Lesson(topicId, order, validTitle, validContent);
    }

    /// <summary>Troca o título e o conteúdo, com as regras da criação. O tópico e a ordem são a chave natural e não mudam.</summary>
    public void Revise(string title, string content) => (Title, Content) = ValidContent(title, content);

    private static (string Title, string Content) ValidContent(string title, string content)
    {
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

        return (trimmedTitle, content);
    }
}
