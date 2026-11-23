using Estudarias.Domain.Students;

namespace Estudarias.Api.Contracts;

/// <summary>
/// O texto do status do tópico no contrato: <c>locked</c>, <c>available</c> ou <c>mastered</c>, em minúsculas. É explícito, e não o
/// <c>ToString()</c> do enum, porque o texto é contrato com o app e renomear um membro não pode mudá-lo sem querer.
/// </summary>
internal static class TopicStatusText
{
    public static string Of(TopicStatus status) => status switch
    {
        TopicStatus.Locked => "locked",
        TopicStatus.Available => "available",
        TopicStatus.Mastered => "mastered",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Topic status is not valid."),
    };
}
