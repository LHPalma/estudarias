using Estudarias.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.FunctionalTests.Support;

/// <summary>
/// Endpoints só dos testes, para provar a política de autenticação sem depender dos controllers reais (que são da #41). Este
/// controller não declara nenhum <c>[Authorize]</c> de propósito: quem protege <c>protected</c> é a política padrão da API.
/// </summary>
[ApiController]
[Route("__test")]
public sealed class TestEndpointsController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("protected")]
    public IActionResult Protected() => Ok(new { userId = currentUser.UserId });

    [HttpGet("open")]
    [AllowAnonymous]
    public IActionResult Open() => Ok(new { open = true });

    [HttpGet("student-only")]
    [Authorize(Roles = "Student")]
    public IActionResult StudentOnly() => Ok(new { role = "Student" });
}
