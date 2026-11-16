using System.Text.RegularExpressions;
using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.ErrorTypes;

public sealed partial class ErrorType : BaseEntity, IAggregateRoot
{
    public const int CodeMaxLength = 100;
    public const int NameMaxLength = 100;

    public Guid TopicId { get; }
    public string Code { get; }
    public string Name { get; }
    public string? Description { get; }
    public string Feedback { get; }

    private ErrorType(Guid topicId, string code, string name, string? description, string feedback)
    {
        TopicId = topicId;
        Code = code;
        Name = name;
        Description = description;
        Feedback = feedback;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*(_[a-z0-9]+)*\z")]
    private static partial Regex CodeFormat();

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

        if (code.Length > CodeMaxLength || !CodeFormat().IsMatch(code))
        {
            throw new DomainValidationException(
                ErrorTypeErrors.CodeInvalid,
                $"Error type code must start with a lowercase letter and have lowercase letters and digits separated by single underscores, with at most {CodeMaxLength} characters.");
        }

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

        return new ErrorType(topicId, code, trimmedName, trimmedDescription, feedback);
    }
}
