using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.ErrorTypes;

public sealed class ErrorType : BaseEntity, IAggregateRoot
{
    public const int CodeMaxLength = ContentCode.MaxLength;
    public const int NameMaxLength = 100;

    public Guid TopicId { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Feedback { get; private set; }

    private ErrorType(Guid topicId, string code, string name, string? description, string feedback)
    {
        TopicId = topicId;
        Code = code;
        Name = name;
        Description = description;
        Feedback = feedback;
    }

    public static ErrorType Create(Guid topicId, string code, string name, string? description, string feedback)
    {
        if (topicId == Guid.Empty)
        {
            throw new DomainValidationException(ErrorTypeErrors.TopicRequired, "Error type topic is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainValidationException(ErrorTypeErrors.CodeRequired, "Error type code is required.");
        }

        if (!ContentCode.IsValid(code))
        {
            throw new DomainValidationException(
                ErrorTypeErrors.CodeInvalid,
                $"Error type code must start with a lowercase letter and have lowercase letters and digits separated by single underscores, with at most {CodeMaxLength} characters.");
        }

        var (validName, validDescription, validFeedback) = ValidContent(name, description, feedback);

        return new ErrorType(topicId, code, validName, validDescription, validFeedback);
    }

    /// <summary>Troca o nome, a descrição e o feedback, com as regras da criação. O tópico e o código são a chave natural e não mudam.</summary>
    public void Revise(string name, string? description, string feedback) =>
        (Name, Description, Feedback) = ValidContent(name, description, feedback);

    private static (string Name, string? Description, string Feedback) ValidContent(string name, string? description, string feedback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(ErrorTypeErrors.NameRequired, "Error type name is required.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainValidationException(
                ErrorTypeErrors.NameTooLong, $"Error type name must have at most {NameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(feedback))
        {
            throw new DomainValidationException(ErrorTypeErrors.FeedbackRequired, "Error type feedback is required.");
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        return (trimmedName, trimmedDescription, feedback);
    }
}
