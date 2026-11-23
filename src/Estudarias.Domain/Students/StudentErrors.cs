using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Students;

public static class StudentErrors
{
    public const string NameRequired = "student.name.required";
    public const string NameTooLong = "student.name.tooLong";
    public const string GroupInvalid = "student.group.invalid";
    public const string TopicLocked = "student.topic.locked";

    /// <summary>
    /// Pedir a lição de um tópico bloqueado (ou, depois, responder um exercício dele): o pedido está certo, o estado do aluno é que
    /// não permite. É 403, ao contrário da violação de invariante do <c>TopicMastery</c>, que é um erro de uso do Domain.
    /// </summary>
    public static DomainForbiddenException LockedTopic() =>
        new(TopicLocked, "The topic is locked: master its prerequisites first.");
}
