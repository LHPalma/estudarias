using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Application.Trail;

/// <summary>
/// Os erros das leituras da trilha: o tópico e a lição que o aluno pediu e que não existem. Os códigos são contrato com o app
/// (<c>docs/api.md</c>), que decide pelo <c>code</c>, e não pela mensagem.
/// </summary>
public static class TrailErrors
{
    public const string TopicNotFound = "trail.topic.notFound";
    public const string LessonNotFound = "trail.lesson.notFound";

    public static DomainNotFoundException NoSuchTopic() => new(TopicNotFound, "The topic does not exist.");

    public static DomainNotFoundException NoSuchLesson() => new(LessonNotFound, "The lesson does not exist.");
}
