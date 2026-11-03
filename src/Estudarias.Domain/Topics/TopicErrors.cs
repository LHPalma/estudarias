namespace Estudarias.Domain.Topics;

public static class TopicErrors
{
    public const string NameRequired = "topic.name.required";
    public const string NameTooLong = "topic.name.tooLong";
    public const string SlugRequired = "topic.slug.required";
    public const string SlugInvalid = "topic.slug.invalid";
    public const string PrerequisiteSelf = "topic.prerequisite.self";
    public const string PrerequisiteDuplicated = "topic.prerequisite.duplicated";
}
