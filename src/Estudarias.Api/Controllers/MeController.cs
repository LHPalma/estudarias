using Estudarias.Api.Contracts.Me;
using Estudarias.Application.Identity;
using Estudarias.Application.Students.Queries;
using Estudarias.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// O perfil de quem está logado. Protegido pela política padrão da API, sem <c>[Authorize]</c> próprio: sem token, ou com token
/// vencido, o middleware do JWT responde 401 antes de chegar aqui. É uma leitura, então não passa por caso de uso: o controller
/// chama a porta de leitura direto (ADR-002).
/// </summary>
[ApiController]
[Route("me")]
[Produces("application/json")]
public sealed class MeController(ICurrentUser currentUser, IProfileReads profiles) : ControllerBase
{
    /// <summary>O perfil do aluno logado: a conta e o perfil de estudo.</summary>
    /// <remarks>
    /// Um token válido de uma conta que não existe mais, ou foi desabilitada, também responde 401 (<c>identity.session.invalid</c>),
    /// sem dizer qual dos dois: para o app, a sessão deixou de valer.
    /// </remarks>
    [HttpGet(Name = "GetMe")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var profile = currentUser.UserId is { } userId ? await profiles.FindAsync(userId, cancellationToken) : null;

        return profile is null ? throw IdentityErrors.InvalidSession() : Ok(MeResponse.From(profile));
    }
}
