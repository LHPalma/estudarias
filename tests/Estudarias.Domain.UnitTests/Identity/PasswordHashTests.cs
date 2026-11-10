using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;

namespace Estudarias.Domain.UnitTests.Identity;

public class PasswordHashTests
{
    private const string Phc = "$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA";

    [Fact(DisplayName = "O hash guarda o texto PHC e a versão do pepper")]
    public void Create_WithValidInput_KeepsValueAndVersion()
    {
        // Act
        var hash = PasswordHash.Create(Phc, 2);

        // Assert
        Assert.Equal(Phc, hash.Value);
        Assert.Equal(2, hash.PepperVersion);
    }

    [Theory(DisplayName = "Um texto que não é PHC não vira hash")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("argon2id$v=19$m=1,t=2,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$c2Fs dA$aGFzaA")]
    public void Create_WithInvalidText_Throws(string? input)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => PasswordHash.Create(input, 1));
        Assert.Equal("identity.passwordHash.invalid", exception.Code);
    }

    [Theory(DisplayName = "A versão do pepper é 1 ou mais")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidPepperVersion_Throws(int version)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => PasswordHash.Create(Phc, version));
        Assert.Equal("identity.passwordHash.invalid", exception.Code);
    }

    [Fact(DisplayName = "TryCreate devolve falso em vez de lançar")]
    public void TryCreate_WithInvalidInput_ReturnsFalse()
    {
        // Act
        var created = PasswordHash.TryCreate("nope", 1, out var hash);

        // Assert
        Assert.False(created);
        Assert.Null(hash);
    }

    [Fact(DisplayName = "O hash nunca aparece ao ser impresso")]
    public void ToString_HidesValue()
    {
        // Act
        var text = PasswordHash.Create(Phc, 1).ToString();

        // Assert
        Assert.DoesNotContain("argon2id", text);
    }
}
