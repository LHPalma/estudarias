using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;

namespace Estudarias.Domain.UnitTests.Identity;

public class PasswordTests
{
    [Fact(DisplayName = "Uma senha que cumpre a política é aceita como está")]
    public void Create_WithValidPassword_KeepsValue()
    {
        // Act
        var password = Password.Create("Abcdef1!");

        // Assert
        Assert.Equal("Abcdef1!", password.Value);
    }

    [Theory(DisplayName = "A senha tem pelo menos 8 caracteres")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Abc1!xy")]
    public void Create_WithShortPassword_ThrowsTooShort(string? input)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Password.Create(input));
        Assert.Equal("identity.password.tooShort", exception.Code);
    }

    [Fact(DisplayName = "A senha não passa de 128 caracteres")]
    public void Create_WithLongPassword_ThrowsTooLong()
    {
        // Arrange
        var input = "Aa1!" + new string('x', 125);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Password.Create(input));
        Assert.Equal("identity.password.tooLong", exception.Code);
    }

    [Fact(DisplayName = "A senha de 128 caracteres é aceita")]
    public void Create_WithPasswordAtMaximum_Succeeds()
    {
        // Arrange
        var input = "Aa1!" + new string('x', 124);

        // Act
        var password = Password.Create(input);

        // Assert
        Assert.Equal(128, password.Value.Length);
    }

    [Theory(DisplayName = "A senha exige maiúscula, minúscula, dígito e caractere especial")]
    [InlineData("abcdef1!", "identity.password.noUppercase")]
    [InlineData("ABCDEF1!", "identity.password.noLowercase")]
    [InlineData("Abcdefg!", "identity.password.noDigit")]
    [InlineData("Abcdefg1", "identity.password.noSpecial")]
    public void Create_MissingACharacterClass_ThrowsMatchingCode(string input, string code)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Password.Create(input));
        Assert.Equal(code, exception.Code);
    }

    [Fact(DisplayName = "A senha nunca aparece em texto puro ao ser impressa")]
    public void ToString_HidesValue()
    {
        // Act
        var text = Password.Create("Abcdef1!").ToString();

        // Assert
        Assert.DoesNotContain("Abcdef1!", text);
    }
}
