using Estudarias.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.ErrorHandling;

/// <summary>
/// Traduz uma exceção de domínio na resposta HTTP, como um <c>ProblemDetails</c> (RFC 9457) com o código do erro em <c>code</c>: é
/// pelo código que o app decide o que mostrar, não pela mensagem. Validação é 400, credencial ou refresh token recusados é 401 e
/// conflito (um e-mail já cadastrado) é 409. Qualquer outra exceção, inclusive uma exceção de domínio sem mapeamento, segue para o
/// tratamento padrão e vira um 500 genérico, sem detalhe técnico para o cliente.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var mapping = exception switch
        {
            DomainValidationException => (Status: StatusCodes.Status400BadRequest, Title: "Validation error"),
            DomainAuthenticationException => (Status: StatusCodes.Status401Unauthorized, Title: "Authentication failed"),
            DomainConflictException => (Status: StatusCodes.Status409Conflict, Title: "Conflict"),
            _ => (Status: 0, Title: ""),
        };

        if (mapping.Status == 0)
        {
            return false;
        }

        var domainException = (BaseDomainException)exception;
        httpContext.Response.StatusCode = mapping.Status;

        // Todo 401 carrega o desafio, como o que o middleware do JWT responde (RFC 9110).
        if (mapping.Status == StatusCodes.Status401Unauthorized)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = mapping.Status,
                Title = mapping.Title,
                Detail = domainException.Message,
                Extensions = { ["code"] = domainException.Code },
            },
        });
    }
}
