using Estudarias.Api.Contracts.Auth;
using Estudarias.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Controllers;

/// <summary>
/// A sessão: cadastro, login, renovação e logout. São os únicos endpoints abertos da API (<c>[AllowAnonymous]</c>), porque são o
/// próprio meio de obter um token. Nada aqui decide regra: cada ação chama o caso de uso, e as falhas de domínio viram
/// <c>ProblemDetails</c> com o código do erro no <c>DomainExceptionHandler</c>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController(
    RegisterStudentHandler register,
    LoginHandler login,
    RefreshSessionHandler refresh,
    LogoutHandler logout) : ControllerBase
{
    /// <summary>O aluno cria a própria conta e já entra logado.</summary>
    [HttpPost("register", Name = "Register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var payload = await register.Handle(
            new RegisterStudentCommand(request.Name!, request.Email!, request.Password!), cancellationToken);

        // Sem Location: ainda não há um recurso de conta para apontar (o perfil e os links entram com o HATEOAS, #8).
        return StatusCode(StatusCodes.Status201Created, AuthResponse.From(payload));
    }

    /// <summary>Troca e-mail e senha por uma sessão nova.</summary>
    [HttpPost("login", Name = "Login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var payload = await login.Handle(new LoginCommand(request.Email!, request.Password!), cancellationToken);
        return Ok(AuthResponse.From(payload));
    }

    /// <summary>Troca um refresh token por um par novo; o token usado deixa de valer.</summary>
    [HttpPost("refresh", Name = "Refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var payload = await refresh.Handle(new RefreshSessionCommand(request.RefreshToken!), cancellationToken);
        return Ok(AuthResponse.From(payload));
    }

    /// <summary>Revoga o refresh token daquele dispositivo. Um token desconhecido, vencido ou já revogado não é erro.</summary>
    [HttpPost("logout", Name = "Logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await logout.Handle(new LogoutCommand(request.RefreshToken!), cancellationToken);
        return NoContent();
    }
}
