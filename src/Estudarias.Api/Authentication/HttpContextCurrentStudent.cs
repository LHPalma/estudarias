using System.Security.Claims;
using Estudarias.Application.Common.Abstractions;

namespace Estudarias.Api.Authentication;

/// <summary>Lê o aluno do usuário autenticado na requisição; só é usado atrás de <c>[Authorize]</c>.</summary>
public sealed class HttpContextCurrentStudent(IHttpContextAccessor accessor) : ICurrentStudent
{
    public Guid Id =>
        Guid.Parse(
            accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("There is no authenticated student in this request."));
}
