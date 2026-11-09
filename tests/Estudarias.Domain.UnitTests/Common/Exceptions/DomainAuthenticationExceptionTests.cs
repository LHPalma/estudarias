using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.UnitTests.Common.Exceptions;

public class DomainAuthenticationExceptionTests
{
    [Fact(DisplayName = "Exceção de autenticação carrega o código e a mensagem")]
    public void Constructor_KeepsCodeAndMessage()
    {
        // Act
        var exception = new DomainAuthenticationException("identity.credentials.invalid", "E-mail or password not valid.");

        // Assert
        Assert.Equal("identity.credentials.invalid", exception.Code);
        Assert.Equal("E-mail or password not valid.", exception.Message);
        Assert.IsAssignableFrom<BaseDomainException>(exception);
    }
}
