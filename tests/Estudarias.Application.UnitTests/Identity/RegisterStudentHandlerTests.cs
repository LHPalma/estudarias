using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Estudarias.Application.UnitTests.Identity;

public class RegisterStudentHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly PasswordHash StoredHash = PasswordHash.Create("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA", 1);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IExperimentalGroupAssigner _groups = Substitute.For<IExperimentalGroupAssigner>();
    private readonly IAccessTokenIssuer _accessTokens = Substitute.For<IAccessTokenIssuer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start);

    private readonly List<Student> _addedStudents = [];
    private readonly List<User> _addedUsers = [];
    private readonly List<RefreshToken> _addedTokens = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public RegisterStudentHandlerTests()
    {
        _hasher.Hash(Arg.Any<Password>()).Returns(StoredHash);
        _groups.Assign().Returns(ExperimentalGroup.Diagnostic);
        _accessTokens.Issue(Arg.Any<User>()).Returns(new IssuedAccessToken("access-token", Start.AddMinutes(15)));
        _students.When(repository => repository.Add(Arg.Any<Student>())).Do(call => _addedStudents.Add(call.Arg<Student>()));
        _users.When(repository => repository.Add(Arg.Any<User>())).Do(call => _addedUsers.Add(call.Arg<User>()));
        _refreshTokens.When(repository => repository.Add(Arg.Any<RefreshToken>())).Do(call => _addedTokens.Add(call.Arg<RefreshToken>()));
    }

    private RegisterStudentHandler Handler() =>
        new(_users, _students, _refreshTokens, _hasher, _groups, _accessTokens, _unitOfWork, _clock);

    private static RegisterStudentCommand Valid(string? email = null) =>
        new("  Ana  ", email ?? "ana@example.com", "Senha-valida-1!");

    private void AssertNothingWasWritten()
    {
        Assert.Empty(_addedStudents);
        Assert.Empty(_addedUsers);
        Assert.Empty(_addedTokens);
        _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O cadastro cria o aluno (nome aparado, no grupo sorteado) e a conta ligada a ele")]
    public async Task Handle_WithValidData_CreatesTheLinkedStudentAndUser()
    {
        // Arrange
        _groups.Assign().Returns(ExperimentalGroup.Control);

        // Act
        await Handler().Handle(Valid(), Cancel);

        // Assert
        var student = Assert.Single(_addedStudents);
        var user = Assert.Single(_addedUsers);
        Assert.Equal("Ana", student.Name);
        Assert.Equal(ExperimentalGroup.Control, student.Group);
        Assert.Equal("ana@example.com", user.Email);
        Assert.Equal(UserRole.Student, user.Role);
        Assert.Equal(student.Id, user.StudentId);
        Assert.True(user.IsActive);
    }

    [Theory(DisplayName = "O grupo do aluno é o que o sorteador decidiu")]
    [InlineData(ExperimentalGroup.Control)]
    [InlineData(ExperimentalGroup.Diagnostic)]
    public async Task Handle_UsesTheGroupFromTheAssigner(ExperimentalGroup group)
    {
        // Arrange
        _groups.Assign().Returns(group);

        // Act
        await Handler().Handle(Valid(), Cancel);

        // Assert
        Assert.Equal(group, Assert.Single(_addedStudents).Group);
    }

    [Fact(DisplayName = "A senha é derivada pelo hasher a partir da senha validada, e só o hash vai para a conta")]
    public async Task Handle_HashesThePasswordAndStoresOnlyTheHash()
    {
        // Act
        await Handler().Handle(Valid(), Cancel);

        // Assert
        _hasher.Received(1).Hash(Arg.Is<Password>(password => password.Value == "Senha-valida-1!"));
        Assert.Equal(StoredHash, Assert.Single(_addedUsers).PasswordHash);
    }

    [Fact(DisplayName = "O cadastro já devolve uma sessão: os dois tokens, a validade do access e a conta")]
    public async Task Handle_ReturnsAnOpenSession()
    {
        // Act
        var payload = await Handler().Handle(Valid(), Cancel);

        // Assert
        Assert.Equal("access-token", payload.AccessToken);
        Assert.Equal(Start.AddMinutes(15), payload.AccessTokenExpiresAt);
        Assert.Matches("^[0-9a-f]{64}$", payload.RefreshToken);
        Assert.Same(Assert.Single(_addedUsers), payload.User);
    }

    [Fact(DisplayName = "O refresh token é gravado só pelo hash, numa família nova, ativo e para a conta criada")]
    public async Task Handle_StoresTheRefreshTokenByHash()
    {
        // Act
        var payload = await Handler().Handle(Valid(), Cancel);

        // Assert
        var token = Assert.Single(_addedTokens);
        Assert.Equal(RefreshTokenSecret.Hash(payload.RefreshToken), token.TokenHash);
        Assert.NotEqual(payload.RefreshToken, token.TokenHash);
        Assert.Equal(Assert.Single(_addedUsers).Id, token.UserId);
        Assert.Equal(Start.AddDays(30), token.ExpiresAt);
        Assert.True(token.IsActive(Start));
    }

    [Fact(DisplayName = "Aluno, conta e token são gravados numa gravação só, depois de marcados")]
    public async Task Handle_SavesOnceAfterMarkingEverything()
    {
        // Act
        await Handler().Handle(Valid(), Cancel);

        // Assert
        Received.InOrder(() =>
        {
            _students.Add(Arg.Any<Student>());
            _users.Add(Arg.Any<User>());
            _refreshTokens.Add(Arg.Any<RefreshToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O e-mail é normalizado (sem espaços e em minúsculas) antes de procurar duplicado e de gravar")]
    public async Task Handle_NormalizesTheEmail()
    {
        // Act
        await Handler().Handle(Valid("  Ana@Example.COM "), Cancel);

        // Assert
        await _users.Received(1).ExistsByEmailAsync("ana@example.com", Arg.Any<CancellationToken>());
        Assert.Equal("ana@example.com", Assert.Single(_addedUsers).Email);
    }

    [Fact(DisplayName = "Um e-mail já cadastrado é conflito, sem sortear, derivar hash nem gravar nada")]
    public async Task Handle_WithTakenEmail_ThrowsConflictAndDoesNothing()
    {
        // Arrange
        _users.ExistsByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainConflictException>(() => Handler().Handle(Valid(), Cancel));
        Assert.Equal("identity.email.taken", exception.Code);
        _groups.DidNotReceive().Assign();
        _hasher.DidNotReceive().Hash(Arg.Any<Password>());
        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "Dois cadastros simultâneos com o mesmo e-mail: o que perde a corrida no índice único também é conflito")]
    public async Task Handle_WhenTheUniqueIndexRejectsTheEmail_ThrowsConflict()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ux_app_user_email", new InvalidOperationException()));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainConflictException>(() => Handler().Handle(Valid(), Cancel));
        Assert.Equal("identity.email.taken", exception.Code);
        _accessTokens.DidNotReceive().Issue(Arg.Any<User>());
    }

    [Theory(DisplayName = "Um e-mail que não é um endereço é recusado antes de qualquer consulta")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("ana@example")]
    public async Task Handle_WithInvalidEmail_ThrowsValidation(string? email)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainValidationException>(
            () => Handler().Handle(new RegisterStudentCommand("Ana", email!, "Senha-valida-1!"), Cancel));
        Assert.Equal("identity.email.invalid", exception.Code);
        await _users.DidNotReceive().ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        AssertNothingWasWritten();
    }

    [Theory(DisplayName = "Uma senha fora da política é recusada antes de qualquer consulta ou custo")]
    [InlineData("curta1!", "identity.password.tooShort")]
    [InlineData("senha-sem-maiuscula-1!", "identity.password.noUppercase")]
    [InlineData("Senha-sem-digito!", "identity.password.noDigit")]
    public async Task Handle_WithWeakPassword_ThrowsValidation(string password, string code)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainValidationException>(
            () => Handler().Handle(new RegisterStudentCommand("Ana", "ana@example.com", password), Cancel));
        Assert.Equal(code, exception.Code);
        await _users.DidNotReceive().ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _hasher.DidNotReceive().Hash(Arg.Any<Password>());
        AssertNothingWasWritten();
    }

    [Theory(DisplayName = "O nome do aluno é obrigatório, e nada é gravado sem ele")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankName_ThrowsValidation(string? name)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainValidationException>(
            () => Handler().Handle(new RegisterStudentCommand(name!, "ana@example.com", "Senha-valida-1!"), Cancel));
        Assert.Equal("student.name.required", exception.Code);
        AssertNothingWasWritten();
    }
}
