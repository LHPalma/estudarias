using Estudarias.Application.Common.Abstractions;
using Estudarias.Application.Students;
using Estudarias.Application.Topics;
using Estudarias.Application.Topics.GetTopic;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Estudarias.Application.UnitTests.Topics.GetTopic;

public class GetTopicHandlerTests
{
    private readonly ITopicRepository _topics = Substitute.For<ITopicRepository>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly ICurrentStudent _currentStudent = Substitute.For<ICurrentStudent>();
    private readonly FakeTimeProvider _clock = new();

    private readonly Topic _topic = Topic.Create("Frações", "fracoes");
    private readonly Student _student = Student.Create("Ana", ExperimentalGroup.Diagnostic);

    public GetTopicHandlerTests()
    {
        _topics.GetByIdAsync(_topic.Id, Arg.Any<CancellationToken>()).Returns(_topic);
        _students.GetByIdAsync(_student.Id, Arg.Any<CancellationToken>()).Returns(_student);
        _currentStudent.Id.Returns(_student.Id);
    }

    private GetTopicHandler NewHandler() => new(_topics, _students, _currentStudent);

    [Fact(DisplayName = "O tópico vem com o status que ele tem para o aluno atual")]
    public async Task HandleAsync_UnlockedTopic_ReturnsTopicWithStudentStatus()
    {
        // Arrange
        _student.Unlock(_topic.Id, _clock);

        // Act
        var details = await NewHandler().HandleAsync(_topic.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(details);
        Assert.Equal(_topic.Id, details.Id);
        Assert.Equal("Frações", details.Name);
        Assert.Equal("fracoes", details.Slug);
        Assert.Equal(TopicStatus.Available, details.Status);
    }

    [Fact(DisplayName = "Um tópico que o aluno ainda não desbloqueou vem bloqueado")]
    public async Task HandleAsync_TopicNotUnlocked_ReturnsLocked()
    {
        // Act
        var details = await NewHandler().HandleAsync(_topic.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(details);
        Assert.Equal(TopicStatus.Locked, details.Status);
    }

    [Fact(DisplayName = "Um tópico inexistente não é encontrado")]
    public async Task HandleAsync_UnknownTopic_ReturnsNull()
    {
        // Act
        var details = await NewHandler().HandleAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(details);
    }

    [Fact(DisplayName = "Sem o aluno atual cadastrado, o tópico não é encontrado")]
    public async Task HandleAsync_UnknownStudent_ReturnsNull()
    {
        // Arrange
        _currentStudent.Id.Returns(Guid.CreateVersion7());

        // Act
        var details = await NewHandler().HandleAsync(_topic.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(details);
    }
}
