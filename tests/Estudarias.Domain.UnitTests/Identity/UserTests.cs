using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;

namespace Estudarias.Domain.UnitTests.Identity;

public class UserTests
{
    private const string Phc = "$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA";

    private static PasswordHash Hash(int version = 1) => PasswordHash.Create(Phc, version);

    [Fact(DisplayName = "Um aluno cadastrado nasce ativo, com o papel aluno e ligado ao seu perfil")]
    public void RegisterStudent_WithValidData_CreatesActiveStudentUser()
    {
        // Arrange
        var studentId = Guid.CreateVersion7();

        // Act
        var user = User.RegisterStudent("ana@example.com", Hash(), studentId);

        // Assert
        Assert.Equal("ana@example.com", user.Email);
        Assert.Equal(UserRole.Student, user.Role);
        Assert.Equal(studentId, user.StudentId);
        Assert.True(user.IsActive);
    }

    [Fact(DisplayName = "O e-mail é guardado sem espaços nas pontas e em minúsculas")]
    public void RegisterStudent_NormalizesEmail()
    {
        // Act
        var user = User.RegisterStudent("  Ana@Example.COM ", Hash(), Guid.CreateVersion7());

        // Assert
        Assert.Equal("ana@example.com", user.Email);
    }

    [Theory(DisplayName = "Um texto que não é um endereço de e-mail é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("@example.com")]
    [InlineData("ana@example")]
    [InlineData("a@b@example.com")]
    [InlineData("an a@example.com")]
    public void RegisterStudent_WithInvalidEmail_ThrowsDomainValidationException(string? email)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => User.RegisterStudent(email!, Hash(), Guid.CreateVersion7()));
        Assert.Equal("identity.email.invalid", exception.Code);
    }

    [Fact(DisplayName = "O e-mail não passa de 254 caracteres")]
    public void TryNormalizeEmail_WithEmailLongerThanLimit_ReturnsFalse()
    {
        // Arrange
        var email = new string('a', 250) + "@b.co";

        // Act
        var valid = User.TryNormalizeEmail(email, out _);

        // Assert
        Assert.False(valid);
    }

    [Fact(DisplayName = "Rehash troca o hash da senha")]
    public void Rehash_ReplacesPasswordHash()
    {
        // Arrange
        var user = User.RegisterStudent("ana@example.com", Hash(1), Guid.CreateVersion7());
        var newer = Hash(2);

        // Act
        user.Rehash(newer);

        // Assert
        Assert.Equal(newer, user.PasswordHash);
    }
}
