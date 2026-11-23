using System.Net;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// GET /lessons/{id} por HTTP contra um PostgreSQL real, com o conteúdo de verdade: a lição sem o gabarito, na ordem do conteúdo, e o
/// acesso do tópico bloqueado. O token vem do cadastro, como o app o obteria.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LessonEndpointTests(PostgresFixture postgres) : ContentEndpointsTests(postgres)
{
    /// <summary>O id da primeira lição do tópico, lido pelo próprio tópico (que abre mesmo bloqueado).</summary>
    private async Task<Guid> FirstLessonOfAsync(JsonElement session, JsonElement trail, string slug) =>
        (await GetJsonAsync(session, $"/topics/{IdOf(trail, slug)}")).GetProperty("lessons")[0].GetProperty("id").GetGuid();

    private async Task<(JsonElement Session, Guid LessonId)> StudentWithAnOpenLessonAsync()
    {
        await LoadContentAsync();
        var session = await RegisterAsync();
        var trail = await TrailAsync(session);
        return (session, await FirstLessonOfAsync(session, trail, "operacoes-basicas"));
    }

    [Fact(DisplayName = "GET /lessons/{id} responde 200 com o conteúdo, o tópico, a ordem e os exercícios com as alternativas")]
    public async Task Lesson_ReturnsTheLessonWithItsExercises()
    {
        // Arrange
        var (session, lessonId) = await StudentWithAnOpenLessonAsync();

        // Act
        var response = await GetWithTokenAsync($"/lessons/{lessonId}", AccessTokenOf(session));
        var lesson = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(lessonId, lesson.GetProperty("id").GetGuid());
        Assert.Equal(1, lesson.GetProperty("order").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(lesson.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(lesson.GetProperty("content").GetString()));
        var exercises = lesson.GetProperty("exercises").EnumerateArray().ToList();
        Assert.NotEmpty(exercises);
        Assert.All(exercises, exercise =>
        {
            Assert.Contains(exercise.GetProperty("difficulty").GetString(), new[] { "easy", "medium", "hard" });
            Assert.True(exercise.GetProperty("alternatives").GetArrayLength() >= 2);
        });
    }

    [Fact(DisplayName = "A lição tem só os campos do contrato: a alternativa só id e texto, o exercício sem correção")]
    public async Task Lesson_ReturnsOnlyTheContractFields()
    {
        // Arrange
        var (session, lessonId) = await StudentWithAnOpenLessonAsync();

        // Act
        var lesson = await GetJsonAsync(session, $"/lessons/{lessonId}");

        // Assert
        Assert.Equal(
            ["content", "exercises", "id", "links", "order", "title", "topicId"],
            lesson.EnumerateObject().Select(property => property.Name).Order());
        var exercise = lesson.GetProperty("exercises")[0];
        Assert.Equal(
            ["alternatives", "difficulty", "id", "links", "statement"],
            exercise.EnumerateObject().Select(property => property.Name).Order());
        Assert.All(
            exercise.GetProperty("alternatives").EnumerateArray(),
            alternative => Assert.Equal(["id", "text"], alternative.EnumerateObject().Select(property => property.Name).Order()));
    }

    [Fact(DisplayName = "RNF-TRL-03: o JSON da lição não tem nenhum campo de correção nem de tipo de erro, em nenhum nível")]
    public async Task Lesson_JsonHasNoCorrectionField()
    {
        // Arrange
        var (session, lessonId) = await StudentWithAnOpenLessonAsync();

        // Act
        var response = await GetWithTokenAsync($"/lessons/{lessonId}", AccessTokenOf(session));
        var body = await response.Content.ReadAsStringAsync(Cancel);

        // Assert: o texto cru, e não só o DTO, porque é a serialização que não pode vazar
        foreach (var forbidden in new[] { "correct", "isCorrect", "errorType", "errorTypeId", "feedback", "answer\":" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "Os exercícios e as alternativas vêm na ordem gravada do conteúdo, a mesma da coluna position")]
    public async Task Lesson_ReturnsTheStoredOrder()
    {
        // Arrange
        var (session, lessonId) = await StudentWithAnOpenLessonAsync();
        var storedStatements = await ReadColumnAsync($"SELECT statement FROM exercise WHERE lesson_id = '{lessonId}' ORDER BY position, id");

        // Act
        var lesson = await GetJsonAsync(session, $"/lessons/{lessonId}");

        // Assert
        var exercises = lesson.GetProperty("exercises").EnumerateArray().ToList();
        Assert.Equal(storedStatements, exercises.Select(exercise => exercise.GetProperty("statement").GetString()!));
        foreach (var exercise in exercises)
        {
            var storedTexts = await ReadColumnAsync(
                $"SELECT text FROM alternative WHERE exercise_id = '{exercise.GetProperty("id").GetGuid()}' ORDER BY position");
            Assert.Equal(storedTexts, exercise.GetProperty("alternatives").EnumerateArray().Select(alternative => alternative.GetProperty("text").GetString()!));
        }
    }

    [Fact(DisplayName = "A lição traz o link self, com a URL absoluta, e o link topic; cada exercício traz o link answer, que é um POST")]
    public async Task Lesson_LinksToItselfAndItsTopic()
    {
        // Arrange
        var (session, lessonId) = await StudentWithAnOpenLessonAsync();

        // Act
        var lesson = await GetJsonAsync(session, $"/lessons/{lessonId}");

        // Assert
        Assert.Equal(["self", "topic"], lesson.GetProperty("links").EnumerateArray().Select(link => link.GetProperty("rel").GetString()));
        Assert.Equal($"http://localhost/lessons/{lessonId}", RelOf(lesson, "self"));
        Assert.Equal($"http://localhost/topics/{lesson.GetProperty("topicId").GetGuid()}", RelOf(lesson, "topic"));
        Assert.Equal(HttpStatusCode.OK, (await GetWithTokenAsync(RelOf(lesson, "topic"), AccessTokenOf(session))).StatusCode);
        Assert.All(lesson.GetProperty("exercises").EnumerateArray(), exercise =>
        {
            var link = Assert.Single(exercise.GetProperty("links").EnumerateArray());
            Assert.Equal(("answer", "POST"), (link.GetProperty("rel").GetString(), link.GetProperty("method").GetString()));
            Assert.Equal($"http://localhost/exercises/{exercise.GetProperty("id").GetGuid()}/attempts", link.GetProperty("href").GetString());
        });
    }

    [Fact(DisplayName = "A lição de um tópico bloqueado responde 403 com student.topic.locked, sem revelar o conteúdo")]
    public async Task Lesson_ForALockedTopic_Returns403()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();
        var trail = await TrailAsync(session);
        var lessonId = await FirstLessonOfAsync(session, trail, "fracoes");

        // Act
        var response = await GetWithTokenAsync($"/lessons/{lessonId}", AccessTokenOf(session));
        var body = await response.Content.ReadAsStringAsync(Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(403, problem.GetProperty("status").GetInt32());
        Assert.Equal("student.topic.locked", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("exercises", body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Liberado o tópico, a lição que respondia 403 passa a abrir")]
    public async Task Lesson_AfterTheTopicIsUnlocked_Opens()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();
        var trail = await TrailAsync(session);
        var lessonId = await FirstLessonOfAsync(session, trail, "fracoes");
        Assert.Equal(HttpStatusCode.Forbidden, (await GetWithTokenAsync($"/lessons/{lessonId}", AccessTokenOf(session))).StatusCode);
        await GrantAsync(session, IdOf(trail, "fracoes"));

        // Act
        var response = await GetWithTokenAsync($"/lessons/{lessonId}", AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Uma lição que não existe responde 404 com trail.lesson.notFound")]
    public async Task Lesson_ForAnUnknownLesson_Returns404()
    {
        // Arrange
        var (session, _) = await StudentWithAnOpenLessonAsync();

        // Act
        var response = await GetWithTokenAsync($"/lessons/{Guid.CreateVersion7()}", AccessTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "trail.lesson.notFound");
    }

    [Fact(DisplayName = "Sem token a lição responde 401 com o desafio Bearer")]
    public async Task Lesson_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.GetAsync($"/lessons/{Guid.CreateVersion7()}", Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "O token de uma conta desabilitada depois do login responde 401 com identity.session.invalid")]
    public async Task Lesson_ForADisabledAccount_Returns401()
    {
        // Arrange
        var (session, lessonId) = await StudentWithAnOpenLessonAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act
        var response = await GetWithTokenAsync($"/lessons/{lessonId}", AccessTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
    }

    private async Task<List<string>> ReadColumnAsync(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancel);
        var values = new List<string>();
        while (await reader.ReadAsync(Cancel))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
