using System.Net;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// GET /topics/{id}/attempts por HTTP contra um PostgreSQL real, com o conteúdo de verdade: o histórico que compõe a nota, as respostas
/// vindas de POST /exercises/{id}/attempts. O token vem do cadastro, como o app o obteria.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TopicAttemptsEndpointTests(PostgresFixture postgres) : ContentEndpointsTests(postgres)
{
    private async Task<(JsonElement Session, JsonElement Trail, Guid ExerciseId)> StudentWithAnExerciseAsync(string email = "ana@example.com")
    {
        await LoadContentAsync();
        var session = await RegisterAsync(email);
        var trail = await TrailAsync(session);
        var topic = await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}");
        var lesson = await GetJsonAsync(session, RelOf(topic.GetProperty("lessons")[0], "lesson"));
        return (session, trail, lesson.GetProperty("exercises")[0].GetProperty("id").GetGuid());
    }

    private async Task AnswerAsync(JsonElement session, Guid exerciseId, bool correct)
    {
        var alternative = await ScalarAsync<Guid>($"SELECT id FROM alternative WHERE exercise_id = '{exerciseId}' AND is_correct = {(correct ? "true" : "false")} ORDER BY position LIMIT 1");
        var response = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId = alternative }, AccessTokenOf(session));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private Task<HttpResponseMessage> HistoryAsync(JsonElement session, Guid topicId) =>
        GetWithTokenAsync($"/topics/{topicId}/attempts", AccessTokenOf(session));

    [Fact(DisplayName = "GET /topics/{id}/attempts responde 200 com as respostas da mais recente para a mais antiga")]
    public async Task Attempts_ReturnsTheAnswersMostRecentFirst()
    {
        // Arrange
        var (session, trail, exerciseId) = await StudentWithAnExerciseAsync();
        await AnswerAsync(session, exerciseId, correct: false);
        await AnswerAsync(session, exerciseId, correct: true);

        // Act
        var response = await HistoryAsync(session, IdOf(trail, "operacoes-basicas"));
        var history = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var items = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([true, false], items.Select(item => item.GetProperty("isCorrect").GetBoolean()));
        Assert.All(items, item => Assert.Equal(exerciseId, item.GetProperty("exerciseId").GetGuid()));
        Assert.True(items[0].GetProperty("answeredAt").GetDateTimeOffset() >= items[1].GetProperty("answeredAt").GetDateTimeOffset());
        var statement = await ScalarAsync<string>($"SELECT statement FROM exercise WHERE id = '{exerciseId}'");
        Assert.Equal(statement, items[0].GetProperty("statement").GetString());
        Assert.Contains(items[0].GetProperty("difficulty").GetString(), new[] { "easy", "medium", "hard" });
    }

    [Fact(DisplayName = "A resposta tem só os campos do contrato: sem a alternativa escolhida nem a correta")]
    public async Task Attempts_ReturnsOnlyTheContractFields()
    {
        // Arrange
        var (session, trail, exerciseId) = await StudentWithAnExerciseAsync();
        await AnswerAsync(session, exerciseId, correct: false);

        // Act
        var response = await HistoryAsync(session, IdOf(trail, "operacoes-basicas"));
        var body = await response.Content.ReadAsStringAsync(Cancel);
        var history = JsonDocument.Parse(body).RootElement;

        // Assert
        Assert.Equal(["items", "links"], history.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(
            ["answeredAt", "attemptId", "difficulty", "exerciseId", "isCorrect", "statement"],
            history.GetProperty("items")[0].EnumerateObject().Select(property => property.Name).Order());
        foreach (var forbidden in new[] { "alternativeId", "correctAlternativeId", "errorType", "feedback" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "Só as 10 respostas mais recentes aparecem, e a nota do tópico se refaz por elas")]
    public async Task Attempts_ReturnsTheScoreWindow()
    {
        // Arrange: 12 respostas, a primeira errada
        var (session, trail, exerciseId) = await StudentWithAnExerciseAsync();
        await AnswerAsync(session, exerciseId, correct: false);
        for (var round = 0; round < 11; round++)
        {
            await AnswerAsync(session, exerciseId, correct: true);
        }

        // Act
        var history = await JsonAsync(await HistoryAsync(session, IdOf(trail, "operacoes-basicas")));
        var topic = await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}");

        // Assert: as 10 mais recentes são todas certas, a errada mais antiga ficou fora, e o tópico conta as 12
        var items = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(10, items.Count);
        Assert.All(items, item => Assert.True(item.GetProperty("isCorrect").GetBoolean()));
        Assert.Equal(12, topic.GetProperty("attemptCount").GetInt32());
        Assert.Equal(100.0m, topic.GetProperty("score").GetDecimal());
    }

    [Fact(DisplayName = "Um tópico liberado sem respostas devolve 200 com a lista vazia")]
    public async Task Attempts_WithoutAnswers_IsEmpty()
    {
        // Arrange
        var (session, trail, _) = await StudentWithAnExerciseAsync();

        // Act
        var response = await HistoryAsync(session, IdOf(trail, "operacoes-basicas"));
        var history = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, history.GetProperty("items").GetArrayLength());
    }

    [Fact(DisplayName = "O histórico traz os links self e topic, com a URL absoluta, e o link attempts do tópico leva até ele")]
    public async Task Attempts_LinksToItselfAndTheTopic()
    {
        // Arrange
        var (session, trail, _) = await StudentWithAnExerciseAsync();
        var topicId = IdOf(trail, "operacoes-basicas");
        var topic = await GetJsonAsync(session, $"/topics/{topicId}");

        // Act
        var history = await JsonAsync(await GetWithTokenAsync(RelOf(topic, "attempts"), AccessTokenOf(session)));

        // Assert
        Assert.Equal(["self", "topic"], history.GetProperty("links").EnumerateArray().Select(link => link.GetProperty("rel").GetString()));
        Assert.Equal($"http://localhost/topics/{topicId}/attempts", RelOf(history, "self"));
        Assert.Equal($"http://localhost/topics/{topicId}", RelOf(history, "topic"));
    }

    [Fact(DisplayName = "As respostas de outro aluno não aparecem")]
    public async Task Attempts_IgnoresTheAnswersOfAnotherStudent()
    {
        // Arrange
        var (ana, trail, exerciseId) = await StudentWithAnExerciseAsync("ana@example.com");
        var bia = await RegisterAsync("bia@example.com", "Bia");
        await AnswerAsync(ana, exerciseId, correct: true);

        // Act
        var biaHistory = await JsonAsync(await HistoryAsync(bia, IdOf(trail, "operacoes-basicas")));
        var anaHistory = await JsonAsync(await HistoryAsync(ana, IdOf(trail, "operacoes-basicas")));

        // Assert
        Assert.Equal(0, biaHistory.GetProperty("items").GetArrayLength());
        Assert.Equal(1, anaHistory.GetProperty("items").GetArrayLength());
    }

    [Fact(DisplayName = "Um tópico bloqueado responde 403 com student.topic.locked")]
    public async Task Attempts_ForALockedTopic_Returns403()
    {
        // Arrange
        var (session, trail, _) = await StudentWithAnExerciseAsync();

        // Act
        var response = await HistoryAsync(session, IdOf(trail, "fracoes"));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "student.topic.locked");
    }

    [Fact(DisplayName = "Um tópico que não existe responde 404 com trail.topic.notFound")]
    public async Task Attempts_ForAnUnknownTopic_Returns404()
    {
        // Arrange
        var (session, _, _) = await StudentWithAnExerciseAsync();

        // Act
        var response = await HistoryAsync(session, Guid.CreateVersion7());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "trail.topic.notFound");
    }

    [Fact(DisplayName = "Sem token o histórico responde 401 com o desafio Bearer")]
    public async Task Attempts_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.GetAsync($"/topics/{Guid.CreateVersion7()}/attempts", Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "Um token válido de uma conta que não existe responde 401 com identity.session.invalid")]
    public async Task Attempts_ForAnUnknownAccount_Returns401WithTheSessionCode()
    {
        // Act
        var response = await GetWithTokenAsync($"/topics/{Guid.CreateVersion7()}/attempts", CraftedTokens.Create(userId: Guid.CreateVersion7()));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
    }

    [Fact(DisplayName = "O histórico é só de leitura: nada é gravado no banco")]
    public async Task Attempts_WritesNothing()
    {
        // Arrange
        var (session, trail, exerciseId) = await StudentWithAnExerciseAsync();
        await AnswerAsync(session, exerciseId, correct: false);
        var before = await ScalarAsync<long>("SELECT (SELECT count(*) FROM student_topic) + (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_flashcard)");

        // Act
        await HistoryAsync(session, IdOf(trail, "operacoes-basicas"));
        await HistoryAsync(session, IdOf(trail, "operacoes-basicas"));

        // Assert
        Assert.Equal(before, await ScalarAsync<long>("SELECT (SELECT count(*) FROM student_topic) + (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_flashcard)"));
    }
}
