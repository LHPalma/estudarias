using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.UnitTests.Common.Exceptions;

public class DomainConflictExceptionTests
{
    [Fact(DisplayName = "Exceção de conflito carrega o código e a mensagem")]
    public void Constructor_KeepsCodeAndMessage()
    {
        // Act
        var exception = new DomainConflictException("identity.email.taken", "There is already a user with this e-mail.");

        // Assert
        Assert.Equal("identity.email.taken", exception.Code);
        Assert.Equal("There is already a user with this e-mail.", exception.Message);
        Assert.IsAssignableFrom<BaseDomainException>(exception);
    }
}
