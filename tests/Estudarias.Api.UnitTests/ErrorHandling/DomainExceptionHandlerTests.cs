using Estudarias.Api.ErrorHandling;
using Estudarias.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Estudarias.Api.UnitTests.ErrorHandling;

public class DomainExceptionHandlerTests
{
    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.FromResult(true);
        }
    }

    /// <summary>Uma exceção de domínio que o handler não conhece: nenhum mapeamento a cobre.</summary>
    private sealed class UnmappedDomainException(string code, string message) : BaseDomainException(code, message);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<(bool Handled, HttpContext Context, CapturingProblemDetailsService Service)> Handle(Exception exception)
    {
        var service = new CapturingProblemDetailsService();
        var context = new DefaultHttpContext();
        var handled = await new DomainExceptionHandler(service).TryHandleAsync(context, exception, Cancel);
        return (handled, context, service);
    }

    [Theory(DisplayName = "RNF-06: cada exceção de domínio vira o status certo, com o código do erro em code")]
    [InlineData(typeof(DomainValidationException), 400, "Validation error")]
    [InlineData(typeof(DomainAuthenticationException), 401, "Authentication failed")]
    [InlineData(typeof(DomainConflictException), 409, "Conflict")]
    public async Task TryHandle_MapsEachDomainExceptionToItsStatus(Type type, int status, string title)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(type, "identity.email.taken", "There is already a user.")!;

        // Act
        var (handled, context, service) = await Handle(exception);

        // Assert
        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        var problem = service.Written!.ProblemDetails;
        Assert.Equal(status, problem.Status);
        Assert.Equal(title, problem.Title);
        Assert.Equal("There is already a user.", problem.Detail);
        Assert.Equal("identity.email.taken", problem.Extensions["code"]);
    }

    [Fact(DisplayName = "RNF-06: todo 401 carrega o desafio Bearer; os outros status não")]
    public async Task TryHandle_OnlyTheUnauthorizedResponseCarriesTheChallenge()
    {
        // Act
        var unauthorized = await Handle(new DomainAuthenticationException("identity.credentials.invalid", "E-mail or password not valid."));
        var conflict = await Handle(new DomainConflictException("identity.email.taken", "Taken."));
        var validation = await Handle(new DomainValidationException("identity.email.invalid", "Not valid."));

        // Assert
        Assert.Equal("Bearer", unauthorized.Context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal(0, conflict.Context.Response.Headers.WWWAuthenticate.Count);
        Assert.Equal(0, validation.Context.Response.Headers.WWWAuthenticate.Count);
    }

    [Fact(DisplayName = "RNF-06: o handler entrega a exceção ao serviço de ProblemDetails, que decide o que expor")]
    public async Task TryHandle_PassesTheExceptionToTheProblemDetailsService()
    {
        // Arrange
        var exception = new DomainValidationException("identity.email.invalid", "Not valid.");

        // Act
        var (_, _, service) = await Handle(exception);

        // Assert
        Assert.Same(exception, service.Written!.Exception);
    }

    [Theory(DisplayName = "RNF-06: o que não é exceção de domínio mapeada não é tratado (vira 500 genérico)")]
    [InlineData("domain-without-mapping")]
    [InlineData("invalid-operation")]
    [InlineData("argument")]
    public async Task TryHandle_WithAnythingElse_ReturnsFalseAndWritesNothing(string kind)
    {
        // Arrange
        Exception exception = kind switch
        {
            "domain-without-mapping" => new UnmappedDomainException("identity.x.y", "Unmapped."),
            "invalid-operation" => new InvalidOperationException("Pepper version 1 is not configured."),
            _ => new ArgumentException("boom"),
        };

        // Act
        var (handled, context, service) = await Handle(exception);

        // Assert
        Assert.False(handled);
        Assert.Null(service.Written);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}
