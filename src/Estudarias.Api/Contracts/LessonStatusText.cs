using Estudarias.Domain.Lessons;

namespace Estudarias.Api.Contracts;

/// <summary>
/// O texto do estado da lição no contrato: <c>notStarted</c>, <c>inProgress</c> ou <c>completed</c>, em camelCase. É explícito, e não o
/// <c>ToString()</c> do enum, porque o texto é contrato com o app e renomear um membro não pode mudá-lo sem querer.
/// </summary>
internal static class LessonStatusText
{
    public static string Of(LessonStatus status) => status switch
    {
        LessonStatus.NotStarted => "notStarted",
        LessonStatus.InProgress => "inProgress",
        LessonStatus.Completed => "completed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Lesson status is not valid."),
    };
}
