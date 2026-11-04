using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Estudarias.Api.Authentication;

/// <summary>
/// Autenticação provisória: o aluno se identifica pelo header <c>X-Student-Id</c>.
/// Sai quando a autenticação por e-mail e senha (UC01) entrar; quem consome o aluno
/// atual depende só de <c>ICurrentStudent</c> e não muda.
/// </summary>
public sealed class StudentHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StudentHeader";
    public const string HeaderName = "X-Student-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var value))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(value, out var studentId))
        {
            return Task.FromResult(AuthenticateResult.Fail($"{HeaderName} is not a valid id."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, studentId.ToString())], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
