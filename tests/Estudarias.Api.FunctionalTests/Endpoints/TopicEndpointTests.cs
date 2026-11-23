using System.Net;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// GET /topics/{id} por HTTP contra um PostgreSQL real, com o conteúdo de verdade: o tópico do aluno logado, o que falta para dominar,
/// as lições e os links. O token vem do cadastro, como o app o obteria.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TopicEndpointTests(PostgresFixture postgres) : ContentEndpointsTests(postgres)
{
    private async Task<(JsonElement Session, JsonElement Trail)> NewStudentAsync(string email = "ana@example.com")
    {
        var session = await RegisterAsync(email);
        return (session, await TrailAsync(session));
    }

    private Task<JsonElement> TopicAsync(JsonElement session, JsonElement trail, string slug) =>
        GetJsonAsync(session, $"/topics/{IdOf(trail, slug)}");

    [Fact(DisplayName = "GET /topics/{id} responde 200 com o tópico disponível, a meta, o mínimo de tentativas e o que ele libera")]
    public async Task Topic_ForAnAvailableTopic_ReturnsTheTopic()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();

        // Act
        var response = await GetWithTokenAsync($"/topics/{IdOf(trail, "operacoes-basicas")}", AccessTokenOf(session));
        var topic = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(("operacoes-basicas", "available"), (topic.GetProperty("slug").GetString(), topic.GetProperty("status").GetString()));
        Assert.Equal(JsonValueKind.Null, topic.GetProperty("score").ValueKind);
        Assert.Equal(0, topic.GetProperty("attemptCount").GetInt32());
        Assert.Equal(70, topic.GetProperty("masteryThreshold").GetDecimal());
        Assert.Equal(5, topic.GetProperty("minAttempts").GetInt32());
        Assert.Equal(0, topic.GetProperty("prerequisites").GetArrayLength());
        var unlocked = Assert.Single(topic.GetProperty("unlocks").EnumerateArray());
        Assert.Equal(IdOf(trail, "fracoes"), unlocked.GetProperty("id").GetGuid());
        Assert.Equal("Frações", unlocked.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "O tópico tem só os campos do contrato, e cada lição só id, ordem, título, estado, exercícios acertados e totais, e links")]
    public async Task Topic_ReturnsOnlyTheContractFields()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();

        // Act
        var topic = await TopicAsync(session, trail, "operacoes-basicas");

        // Assert
        Assert.Equal(
            ["attemptCount", "id", "lessons", "links", "masteryThreshold", "minAttempts", "name", "prerequisites", "score", "slug", "status", "unlocks"],
            topic.EnumerateObject().Select(property => property.Name).Order());
        var lesson = topic.GetProperty("lessons")[0];
        Assert.Equal(
            ["completedExercises", "id", "links", "order", "status", "title", "totalExercises"],
            lesson.EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact(DisplayName = "O tópico bloqueado abre (200), mostra o que falta e traz as lições sem o link lesson")]
    public async Task Topic_ForALockedTopic_OpensWithoutLessonLinks()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();

        // Act
        var response = await GetWithTokenAsync($"/topics/{IdOf(trail, "fracoes")}", AccessTokenOf(session));
        var topic = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("locked", topic.GetProperty("status").GetString());
        var prerequisite = Assert.Single(topic.GetProperty("prerequisites").EnumerateArray());
        Assert.Equal(("Operações básicas", "available"), (prerequisite.GetProperty("name").GetString(), prerequisite.GetProperty("status").GetString()));
        var lessons = topic.GetProperty("lessons").EnumerateArray().ToList();
        Assert.NotEmpty(lessons);
        Assert.All(lessons, lesson => Assert.Equal(0, lesson.GetProperty("links").GetArrayLength()));
    }

    [Fact(DisplayName = "No tópico liberado, cada lição traz o link lesson, e ele abre a lição")]
    public async Task Topic_ForAnAvailableTopic_LessonLinksOpenTheLessons()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();
        var topic = await TopicAsync(session, trail, "operacoes-basicas");

        // Act & Assert
        foreach (var lesson in topic.GetProperty("lessons").EnumerateArray())
        {
            var link = Assert.Single(lesson.GetProperty("links").EnumerateArray());
            Assert.Equal(("lesson", "GET"), (link.GetProperty("rel").GetString(), link.GetProperty("method").GetString()));
            Assert.Equal($"http://localhost/lessons/{lesson.GetProperty("id").GetGuid()}", link.GetProperty("href").GetString());
            Assert.Equal(HttpStatusCode.OK, (await GetWithTokenAsync(link.GetProperty("href").GetString()!, AccessTokenOf(session))).StatusCode);
        }
    }

    [Fact(DisplayName = "Sem tentativas, cada lição não foi iniciada, com 0 exercícios acertados dos que ela tem")]
    public async Task Topic_WithoutAttempts_EveryLessonIsNotStarted()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();

        // Act
        var topic = await TopicAsync(session, trail, "operacoes-basicas");

        // Assert
        foreach (var lesson in topic.GetProperty("lessons").EnumerateArray())
        {
            Assert.Equal("notStarted", lesson.GetProperty("status").GetString());
            Assert.Equal(0, lesson.GetProperty("completedExercises").GetInt32());
            var exercises = await CountAsync("exercise", $"lesson_id = '{lesson.GetProperty("id").GetGuid()}'");
            Assert.Equal(exercises, lesson.GetProperty("totalExercises").GetInt32());
        }
    }

    [Fact(DisplayName = "Responder muda o estado da lição: em andamento, e concluída quando todos os exercícios foram acertados; a trilha acompanha")]
    public async Task Topic_AfterAnswering_ShowsTheLessonStateAndTheTrailCounts()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();
        var topicBefore = await TopicAsync(session, trail, "operacoes-basicas");
        var lessonId = topicBefore.GetProperty("lessons")[0].GetProperty("id").GetGuid();
        var exerciseIds = new List<Guid>();
        await using (var connection = new Npgsql.NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync(Cancel);
            await using var command = new Npgsql.NpgsqlCommand($"SELECT id FROM exercise WHERE lesson_id = '{lessonId}' ORDER BY position", connection);
            await using var reader = await command.ExecuteReaderAsync(Cancel);
            while (await reader.ReadAsync(Cancel))
            {
                exerciseIds.Add(reader.GetGuid(0));
            }
        }

        async Task AnswerAsync(Guid exerciseId, bool correct)
        {
            var alternative = await ScalarAsync<Guid>($"SELECT id FROM alternative WHERE exercise_id = '{exerciseId}' AND is_correct = {(correct ? "true" : "false")} ORDER BY position LIMIT 1");
            var response = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId = alternative }, AccessTokenOf(session));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        async Task<JsonElement> LessonAsync() =>
            (await TopicAsync(session, trail, "operacoes-basicas")).GetProperty("lessons").EnumerateArray()
                .Single(lesson => lesson.GetProperty("id").GetGuid() == lessonId);

        // Act & Assert: errar o primeiro deixa a lição em andamento, 0 acertados
        await AnswerAsync(exerciseIds[0], correct: false);
        var afterWrong = await LessonAsync();
        Assert.Equal(("inProgress", 0), (afterWrong.GetProperty("status").GetString(), afterWrong.GetProperty("completedExercises").GetInt32()));

        // Acertar todos conclui a lição
        foreach (var exerciseId in exerciseIds)
        {
            await AnswerAsync(exerciseId, correct: true);
        }

        var done = await LessonAsync();
        Assert.Equal(("completed", exerciseIds.Count, exerciseIds.Count), (done.GetProperty("status").GetString(), done.GetProperty("completedExercises").GetInt32(), done.GetProperty("totalExercises").GetInt32()));
        var after = await TrailAsync(session);
        Assert.Equal(1, TopicOf(after, "operacoes-basicas").GetProperty("completedLessons").GetInt32());
    }

    [Fact(DisplayName = "As lições vêm por ordem, a partir de 1")]
    public async Task Topic_ListsTheLessonsInOrder()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();

        // Act
        var topic = await TopicAsync(session, trail, "fracoes");

        // Assert
        var orders = topic.GetProperty("lessons").EnumerateArray().Select(lesson => lesson.GetProperty("order").GetInt32()).ToList();
        Assert.Equal(Enumerable.Range(1, orders.Count), orders);
    }

    [Fact(DisplayName = "O tópico traz o link self, com a URL absoluta, e o link trail")]
    public async Task Topic_LinksToItselfAndTheTrail()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();
        var id = IdOf(trail, "operacoes-basicas");

        // Act
        var topic = await TopicAsync(session, trail, "operacoes-basicas");

        // Assert
        Assert.Equal(["self", "trail"], topic.GetProperty("links").EnumerateArray().Select(link => link.GetProperty("rel").GetString()));
        Assert.Equal($"http://localhost/topics/{id}", RelOf(topic, "self"));
        Assert.Equal("http://localhost/trail", RelOf(topic, "trail"));
        var followed = await JsonAsync(await GetWithTokenAsync(RelOf(topic, "self"), AccessTokenOf(session)));
        Assert.Equal(topic.GetRawText(), followed.GetRawText());
    }

    [Fact(DisplayName = "Liberado o tópico, o status muda para available e o pré-requisito dominado aparece como mastered")]
    public async Task Topic_AfterTheUnlock_ShowsTheNewStatuses()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();
        await GrantAsync(session, IdOf(trail, "operacoes-basicas"), mastered: true);
        await GrantAsync(session, IdOf(trail, "fracoes"));

        // Act
        var topic = await TopicAsync(session, trail, "fracoes");

        // Assert
        Assert.Equal("available", topic.GetProperty("status").GetString());
        Assert.Equal("mastered", topic.GetProperty("prerequisites")[0].GetProperty("status").GetString());
        Assert.True(topic.GetProperty("lessons")[0].GetProperty("links").GetArrayLength() > 0);
    }

    [Fact(DisplayName = "Cada aluno vê o status do tópico para si, e não o de outro")]
    public async Task Topic_WithTwoStudents_ShowsEachOwnStatus()
    {
        // Arrange
        await LoadContentAsync();
        var (ana, trail) = await NewStudentAsync("ana@example.com");
        var (bia, _) = await NewStudentAsync("bia@example.com");
        await GrantAsync(ana, IdOf(trail, "operacoes-basicas"), mastered: true);

        // Act
        var anaTopic = await TopicAsync(ana, trail, "operacoes-basicas");
        var biaTopic = await TopicAsync(bia, trail, "operacoes-basicas");

        // Assert
        Assert.Equal("mastered", anaTopic.GetProperty("status").GetString());
        Assert.Equal("available", biaTopic.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Um tópico que não existe responde 404 com trail.topic.notFound")]
    public async Task Topic_ForAnUnknownTopic_Returns404()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();

        // Act
        var response = await GetWithTokenAsync($"/topics/{Guid.CreateVersion7()}", AccessTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "trail.topic.notFound");
    }

    [Fact(DisplayName = "Um id que não é GUID não casa com a rota: 404 sem corpo de erro de domínio")]
    public async Task Topic_WithAMalformedId_Returns404()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var response = await GetWithTokenAsync("/topics/nao-e-um-guid", AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Sem token o tópico responde 401 com o desafio Bearer")]
    public async Task Topic_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.GetAsync($"/topics/{Guid.CreateVersion7()}", Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "Um token válido de uma conta que não existe responde 401 com identity.session.invalid")]
    public async Task Topic_ForAnUnknownAccount_Returns401WithTheSessionCode()
    {
        // Act
        var response = await GetWithTokenAsync($"/topics/{Guid.CreateVersion7()}", CraftedTokens.Create(userId: Guid.CreateVersion7()));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
    }

    [Fact(DisplayName = "O tópico é só de leitura: nada é gravado no banco")]
    public async Task Topic_WritesNothing()
    {
        // Arrange
        await LoadContentAsync();
        var (session, trail) = await NewStudentAsync();
        var before = await ScalarAsync<long>("SELECT (SELECT count(*) FROM student_topic) + (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_flashcard)");

        // Act
        await TopicAsync(session, trail, "operacoes-basicas");
        await TopicAsync(session, trail, "fracoes");

        // Assert
        Assert.Equal(before, await ScalarAsync<long>("SELECT (SELECT count(*) FROM student_topic) + (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_flashcard)"));
    }
}
