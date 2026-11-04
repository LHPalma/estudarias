using System.Net;
using System.Net.Http.Json;
using Estudarias.Api.Authentication;
using Estudarias.Api.FunctionalTests.Infrastructure;
using Estudarias.Api.Links;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Api.FunctionalTests.Topics;

public class TopicsControllerTests(EstudariasApiFactory factory) : IClassFixture<EstudariasApiFactory>
{
    private sealed record TopicBody(Guid Id, string Name, string Slug, string Status, List<Link> Links);

    private readonly FakeTimeProvider _clock = new();

    private (Topic Topic, Student Student) Seed()
    {
        var topic = Topic.Create("Frações", "fracoes");
        var student = Student.Create("Ana", ExperimentalGroup.Diagnostic);
        factory.Topics.Add(topic);
        factory.Students.Add(student);
        return (topic, student);
    }

    private HttpClient ClientFor(Student student)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(StudentHeaderAuthenticationHandler.HeaderName, student.Id.ToString());
        return client;
    }

    [Fact(DisplayName = "Todo tópico traz o link self com a URL absoluta dele")]
    public async Task Get_AnyTopic_IncludesSelfLink()
    {
        // Arrange
        var (topic, student) = Seed();

        // Act
        var body = await ClientFor(student)
            .GetFromJsonAsync<TopicBody>($"/topics/{topic.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(body);
        var self = Assert.Single(body.Links, link => link.Rel == "self");
        Assert.Equal($"http://localhost/topics/{topic.Id}", self.Href);
        Assert.Equal("GET", self.Method);
    }

    [Fact(DisplayName = "Um tópico desbloqueado oferece o link para começar a lição")]
    public async Task Get_UnlockedTopic_IncludesStartLessonLink()
    {
        // Arrange
        var (topic, student) = Seed();
        student.Unlock(topic.Id, _clock);

        // Act
        var body = await ClientFor(student)
            .GetFromJsonAsync<TopicBody>($"/topics/{topic.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(body);
        Assert.Equal("available", body.Status);
        var startLesson = Assert.Single(body.Links, link => link.Rel == "start-lesson");
        Assert.Equal($"http://localhost/topics/{topic.Id}/lessons", startLesson.Href);
        Assert.Equal("POST", startLesson.Method);
    }

    [Fact(DisplayName = "Um tópico bloqueado não oferece o link para começar a lição")]
    public async Task Get_LockedTopic_OmitsStartLessonLink()
    {
        // Arrange
        var (topic, student) = Seed();

        // Act
        var body = await ClientFor(student)
            .GetFromJsonAsync<TopicBody>($"/topics/{topic.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(body);
        Assert.Equal("locked", body.Status);
        Assert.DoesNotContain(body.Links, link => link.Rel == "start-lesson");
    }

    [Fact(DisplayName = "Sem identificar o aluno, a API responde 401")]
    public async Task Get_WithoutStudentHeader_ReturnsUnauthorized()
    {
        // Arrange
        var (topic, _) = Seed();

        // Act
        var response = await factory.CreateClient()
            .GetAsync($"/topics/{topic.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Um tópico inexistente responde 404")]
    public async Task Get_UnknownTopic_ReturnsNotFound()
    {
        // Arrange
        var (_, student) = Seed();

        // Act
        var response = await ClientFor(student)
            .GetAsync($"/topics/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
