using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.UnitTests.Common.Exceptions;

public class DomainValidationExceptionTests
{
    [Fact(DisplayName = "Exceção de validação carrega o código e a mensagem")]
    public void Constructor_KeepsCodeAndMessage()
    {
        // Act
        var exception = new DomainValidationException("topic.name.required", "Topic name is required.");

        // Assert
        Assert.Equal("topic.name.required", exception.Code);
        Assert.Equal("Topic name is required.", exception.Message);
        Assert.IsAssignableFrom<BaseDomainException>(exception);
    }
}
