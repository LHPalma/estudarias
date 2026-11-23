using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.Students;
using Estudarias.Application.Topics;
using Estudarias.Application.Trail;
using Estudarias.Application.UnitTests.Support;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Estudarias.Application.UnitTests.Trail;

public class UnlockInitialTopicsHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly ITopicRepository _topics = Substitute.For<ITopicRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start);

    private readonly Student _student = Student.Create("Ana", ExperimentalGroup.Diagnostic);
    private readonly User _user;

    private readonly Topic _basics = Topic.Create("Operações básicas", "operacoes-basicas");
    private readonly Topic _fractions = Topic.Create("Frações", "fracoes");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public UnlockInitialTopicsHandlerTests()
    {
        _fractions.AddPrerequisite(_basics.Id);

        _user = User.RegisterStudent("ana@example.com", TestUsers.Hash, _student.Id);
        _users.FindByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _students.FindAsync(_student.Id, Arg.Any<CancellationToken>()).Returns(_student);
        _topics.ListAsync(Arg.Any<CancellationToken>()).Returns([_basics, _fractions]);
    }

    private UnlockInitialTopicsHandler Handler() => new(_users, _students, _topics, _unitOfWork, _clock);

    [Fact(DisplayName = "Um aluno novo tem só os tópicos sem pré-requisito liberados, e o progresso é gravado")]
    public async Task Handle_ForANewStudent_UnlocksTheRootsAndSaves()
    {
        // Act
        await Handler().Handle(_user.Id, Cancel);

        // Assert
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_basics.Id));
        Assert.Equal(TopicStatus.Locked, _student.StatusOf(_fractions.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "Quem já tem a raiz liberada não grava nada")]
    public async Task Handle_WhenTheRootsAreAlreadyUnlocked_DoesNotSave()
    {
        // Arrange
        _student.Unlock(_basics.Id, _clock);

        // Act
        await Handler().Handle(_user.Id, Cancel);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um tópico que o aluno já domina continua dominado")]
    public async Task Handle_KeepsAMasteredTopicMastered()
    {
        // Arrange
        _student.Unlock(_basics.Id, _clock);
        _student.MarkMastered(_basics.Id, _clock);

        // Act
        await Handler().Handle(_user.Id, Cancel);

        // Assert
        Assert.Equal(TopicStatus.Mastered, _student.StatusOf(_basics.Id));
    }

    [Fact(DisplayName = "Conteúdo novo sem pré-requisito é liberado para quem já tinha a trilha")]
    public async Task Handle_WhenTheSeedAddedARootTopic_UnlocksOnlyTheNewOne()
    {
        // Arrange
        var geometry = Topic.Create("Geometria", "geometria");
        _topics.ListAsync(Arg.Any<CancellationToken>()).Returns([_basics, _fractions, geometry]);
        _student.Unlock(_basics.Id, _clock);

        // Act
        await Handler().Handle(_user.Id, Cancel);

        // Assert
        Assert.Equal(TopicStatus.Available, _student.StatusOf(geometry.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "Sem nenhum tópico carregado, não há o que liberar nem gravar")]
    public async Task Handle_WithoutContent_DoesNothing()
    {
        // Arrange
        _topics.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await Handler().Handle(_user.Id, Cancel);

        // Assert
        Assert.Empty(_student.Progress);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Duas aberturas ao mesmo tempo: quem perde a corrida pelo índice único não falha")]
    public async Task Handle_WhenAnotherRequestUnlockedFirst_Succeeds()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ux_student_topic_student_id_topic_id", new InvalidOperationException()));

        // Act
        var exception = await Record.ExceptionAsync(() => Handler().Handle(_user.Id, Cancel));

        // Assert
        Assert.Null(exception);
    }

    [Fact(DisplayName = "Uma conta desconhecida, desabilitada ou sem aluno é sessão inválida")]
    public async Task Handle_ForAnInvalidAccount_ThrowsInvalidSession()
    {
        // Arrange
        var unknown = Guid.CreateVersion7();
        _users.FindByIdAsync(unknown, Arg.Any<CancellationToken>()).Returns((User?)null);
        var inactive = TestUsers.Inactive();
        _users.FindByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);

        // Act & Assert
        foreach (var userId in new[] { unknown, inactive.Id })
        {
            var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => Handler().Handle(userId, Cancel));
            Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
        }

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Uma conta cujo aluno sumiu é sessão inválida")]
    public async Task Handle_WhenTheStudentIsMissing_ThrowsInvalidSession()
    {
        // Arrange
        _students.FindAsync(_student.Id, Arg.Any<CancellationToken>()).Returns((Student?)null);

        // Act
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => Handler().Handle(_user.Id, Cancel));

        // Assert
        Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
    }
}
