namespace Estudarias.Domain.Common.Exceptions;

/// <summary>
/// O aluno está autenticado e o pedido está certo, mas o estado dele não permite a ação: praticar um tópico que ainda está bloqueado.
/// Diferente de 401 (quem é você) e de 400 (o pedido está errado).
/// </summary>
public sealed class DomainForbiddenException(string code, string message)
    : BaseDomainException(code, message);
