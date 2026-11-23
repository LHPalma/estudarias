using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// POST /exercises/{id}/attempts por HTTP contra um PostgreSQL real, com o conteúdo de verdade: o ciclo inteiro (errar, diagnosticar,
/// cartão, nota, desbloqueio) e as recusas. O token vem do cadastro, como o app o obteria.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AttemptEndpointTests(PostgresFixture postgres) : ContentEndpointsTests(postgres)
{
    private async Task<(JsonElement Session, JsonElement Trail, Guid ExerciseId)> StudentWithAnExerciseAsync(
        string email = "ana@example.com", string group = "diagnostic")
    {
        await LoadContentAsync();
        var session = await RegisterAsync(email);
        await ExecuteAsync($"UPDATE student SET experimental_group = '{group}' WHERE id = '{StudentIdOf(session)}'");
        var trail = await TrailAsync(session);
        var topic = await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}");
        var lesson = await GetJsonAsync(session, RelOf(topic.GetProperty("lessons")[0], "lesson"));
        return (session, trail, lesson.GetProperty("exercises")[0].GetProperty("id").GetGuid());
    }

    private static Guid StudentIdOf(JsonElement session) => session.GetProperty("user").GetProperty("studentId").GetGuid();

    private Task<Guid> AlternativeAsync(Guid exerciseId, bool correct) => ScalarAsync<Guid>(
        $"SELECT id FROM alternative WHERE exercise_id = '{exerciseId}' AND is_correct = {(correct ? "true" : "false")} ORDER BY position LIMIT 1");

    private Task<HttpResponseMessage> AnswerAsync(JsonElement session, Guid exerciseId, Guid alternativeId, int? seconds = 15) =>
        PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId, responseSeconds = seconds }, AccessTokenOf(session));

    private async Task<JsonElement> AnswerOkAsync(JsonElement session, Guid exerciseId, bool correct)
    {
        var response = await AnswerAsync(session, exerciseId, await AlternativeAsync(exerciseId, correct));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    [Fact(DisplayName = "Acertar responde 201 com o retorno correct, a nota 100, sem tipo de erro e sem cartão")]
    public async Task Attempt_WithTheRightAlternative_Returns201()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();
        var correct = await AlternativeAsync(exerciseId, correct: true);

        // Act
        var response = await AnswerAsync(session, exerciseId, correct);
        var result = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(result.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(correct, result.GetProperty("correctAlternativeId").GetGuid());
        var feedback = result.GetProperty("feedback");
        Assert.Equal("correct", feedback.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, feedback.GetProperty("errorType").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("flashcard").ValueKind);
        var topic = result.GetProperty("topic");
        Assert.Equal(("available", 100.0m, 1), (topic.GetProperty("status").GetString(), topic.GetProperty("score").GetDecimal(), topic.GetProperty("attemptCount").GetInt32()));
        Assert.Equal(0, result.GetProperty("unlockedTopics").GetArrayLength());
        Assert.Equal(1, await CountAsync("attempt"));
    }

    [Fact(DisplayName = "A resposta tem só os campos do contrato, e cada bloco só os seus")]
    public async Task Attempt_ReturnsOnlyTheContractFields()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();

        // Act
        var result = await AnswerOkAsync(session, exerciseId, correct: false);

        // Assert
        Assert.Equal(
            ["attemptId", "correctAlternativeId", "feedback", "flashcard", "isCorrect", "links", "topic", "unlockedTopics"],
            result.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["correctAnswer", "errorType", "kind"], result.GetProperty("feedback").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["feedback", "name"], result.GetProperty("feedback").GetProperty("errorType").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["attemptCount", "id", "score", "status"], result.GetProperty("topic").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["id", "nextReviewAt"], result.GetProperty("flashcard").EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact(DisplayName = "Errar no grupo diagnóstico devolve o tipo de erro do conteúdo e cria o cartão, gravado com a tentativa de origem")]
    public async Task Attempt_WithAWrongAlternative_InTheDiagnosticGroup_ReturnsTheDiagnosisAndTheCard()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync(group: "diagnostic");
        var wrong = await AlternativeAsync(exerciseId, correct: false);
        var expectedName = await ScalarAsync<string>($"SELECT e.name FROM alternative a JOIN error_type e ON e.id = a.error_type_id WHERE a.id = '{wrong}'");
        var expectedFeedback = await ScalarAsync<string>($"SELECT e.feedback FROM alternative a JOIN error_type e ON e.id = a.error_type_id WHERE a.id = '{wrong}'");

        // Act
        var response = await AnswerAsync(session, exerciseId, wrong);
        var result = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(result.GetProperty("isCorrect").GetBoolean());
        var feedback = result.GetProperty("feedback");
        Assert.Equal("incorrectWithDiagnosis", feedback.GetProperty("kind").GetString());
        Assert.Equal(expectedName, feedback.GetProperty("errorType").GetProperty("name").GetString());
        Assert.Equal(expectedFeedback, feedback.GetProperty("errorType").GetProperty("feedback").GetString());
        var card = result.GetProperty("flashcard");
        Assert.True(card.GetProperty("nextReviewAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddHours(-1));
        Assert.Equal(
            result.GetProperty("attemptId").GetGuid(),
            await ScalarAsync<Guid>($"SELECT origin_attempt_id FROM student_flashcard WHERE id = '{card.GetProperty("id").GetGuid()}'"));
    }

    [Fact(DisplayName = "ADR-005: no grupo controle o retorno é incorrect, sem o tipo de erro, e o cartão é criado igual")]
    public async Task Attempt_WithAWrongAlternative_InTheControlGroup_HidesTheDiagnosisButCreatesTheCard()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync(group: "control");

        // Act
        var result = await AnswerOkAsync(session, exerciseId, correct: false);

        // Assert
        var feedback = result.GetProperty("feedback");
        Assert.Equal("incorrect", feedback.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, feedback.GetProperty("errorType").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(feedback.GetProperty("correctAnswer").GetString()));
        Assert.Equal(JsonValueKind.Object, result.GetProperty("flashcard").ValueKind);
        Assert.Equal(1, await CountAsync("student_flashcard"));
    }

    [Fact(DisplayName = "RN-RSP-05: a estrutura da resposta é a mesma nos dois grupos; só o conteúdo do feedback muda")]
    public async Task Attempt_HasTheSameShapeInBothGroups()
    {
        // Arrange
        var (diagnostic, _, diagnosticExercise) = await StudentWithAnExerciseAsync("ana@example.com", "diagnostic");
        var control = await RegisterAsync("bia@example.com", "Bia");
        await ExecuteAsync($"UPDATE student SET experimental_group = 'control' WHERE id = '{StudentIdOf(control)}'");

        // Act
        var diagnosticResult = await AnswerOkAsync(diagnostic, diagnosticExercise, correct: false);
        var controlResult = await AnswerOkAsync(control, diagnosticExercise, correct: false);

        // Assert
        static IEnumerable<string> Names(JsonElement element) => element.EnumerateObject().Select(property => property.Name).Order();
        Assert.Equal(Names(diagnosticResult), Names(controlResult));
        Assert.Equal(Names(diagnosticResult.GetProperty("feedback")), Names(controlResult.GetProperty("feedback")));
        Assert.Equal(diagnosticResult.GetProperty("correctAlternativeId").GetGuid(), controlResult.GetProperty("correctAlternativeId").GetGuid());
        Assert.Equal(
            diagnosticResult.GetProperty("feedback").GetProperty("correctAnswer").GetString(),
            controlResult.GetProperty("feedback").GetProperty("correctAnswer").GetString());
    }

    [Fact(DisplayName = "RN-RSP-13: o grupo vem do aluno gravado, e um campo experimentalGroup no corpo é ignorado")]
    public async Task Attempt_IgnoresAGroupSentInTheBody()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync(group: "diagnostic");
        var wrong = await AlternativeAsync(exerciseId, correct: false);

        // Act
        var response = await PostWithTokenAsync(
            $"/exercises/{exerciseId}/attempts", new { alternativeId = wrong, experimentalGroup = "control", studentId = Guid.CreateVersion7() }, AccessTokenOf(session));
        var result = await JsonAsync(response);

        // Assert
        Assert.Equal("incorrectWithDiagnosis", result.GetProperty("feedback").GetProperty("kind").GetString());
        Assert.Equal(StudentIdOf(session), await ScalarAsync<Guid>($"SELECT student_id FROM attempt WHERE id = '{result.GetProperty("attemptId").GetGuid()}'"));
    }

    [Fact(DisplayName = "Errar de novo o mesmo tipo de erro grava a tentativa e não cria outro cartão: flashcard nulo")]
    public async Task Attempt_TheSameErrorTwice_CreatesOnlyOneCard()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();

        // Act
        var first = await AnswerOkAsync(session, exerciseId, correct: false);
        var second = await AnswerOkAsync(session, exerciseId, correct: false);

        // Assert
        Assert.Equal(JsonValueKind.Object, first.GetProperty("flashcard").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("flashcard").ValueKind);
        Assert.Equal(2, await CountAsync("attempt"));
        Assert.Equal(1, await CountAsync("student_flashcard"));
        Assert.Equal(2, second.GetProperty("topic").GetProperty("attemptCount").GetInt32());
    }

    [Fact(DisplayName = "O ciclo: errar, cartão criado, acertar até dominar e o dependente liberado, visto pela trilha")]
    public async Task Attempt_TheWholeCycle_MastersTheTopicAndUnlocksTheNext()
    {
        // Arrange
        var (session, trail, exerciseId) = await StudentWithAnExerciseAsync();
        Assert.Equal("locked", TopicOf(trail, "fracoes").GetProperty("status").GetString());

        // Act: erra uma vez (cartão), e acerta até o tópico ser dominado
        var first = await AnswerOkAsync(session, exerciseId, correct: false);
        JsonElement last = first;
        for (var round = 0; round < 12 && last.GetProperty("topic").GetProperty("status").GetString() != "mastered"; round++)
        {
            last = await AnswerOkAsync(session, exerciseId, correct: true);
        }

        // Assert
        Assert.Equal(JsonValueKind.Object, first.GetProperty("flashcard").ValueKind);
        Assert.Equal("mastered", last.GetProperty("topic").GetProperty("status").GetString());
        Assert.True(last.GetProperty("topic").GetProperty("score").GetDecimal() >= 70m);
        Assert.True(last.GetProperty("topic").GetProperty("attemptCount").GetInt32() >= 5);
        var unlocked = Assert.Single(last.GetProperty("unlockedTopics").EnumerateArray());
        Assert.Equal(IdOf(trail, "fracoes"), unlocked.GetProperty("id").GetGuid());
        Assert.Equal("Frações", unlocked.GetProperty("name").GetString());

        var after = await TrailAsync(session);
        Assert.Equal("mastered", TopicOf(after, "operacoes-basicas").GetProperty("status").GetString());
        Assert.Equal("available", TopicOf(after, "fracoes").GetProperty("status").GetString());
        Assert.Equal(1, after.GetProperty("masteredTopics").GetInt32());
        Assert.Equal(1, await CountAsync("student_flashcard"));
        Assert.Equal(0, after.GetProperty("dueReviews").GetInt32());
    }

    [Fact(DisplayName = "A resposta traz o link topic, que leva ao tópico com a nota nova")]
    public async Task Attempt_LinksToTheTopic()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();

        // Act
        var result = await AnswerOkAsync(session, exerciseId, correct: true);

        // Assert
        var link = Assert.Single(result.GetProperty("links").EnumerateArray());
        Assert.Equal(("topic", "GET"), (link.GetProperty("rel").GetString(), link.GetProperty("method").GetString()));
        var topic = await JsonAsync(await GetWithTokenAsync(link.GetProperty("href").GetString()!, AccessTokenOf(session)));
        Assert.Equal(result.GetProperty("topic").GetProperty("score").GetDecimal(), topic.GetProperty("score").GetDecimal());
        Assert.Equal(1, topic.GetProperty("attemptCount").GetInt32());
    }

    [Fact(DisplayName = "O link answer da lição leva a este endpoint")]
    public async Task Attempt_IsReachableFromTheLessonAnswerLink()
    {
        // Arrange
        var (session, trail, _) = await StudentWithAnExerciseAsync();
        var topic = await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}");
        var lesson = await GetJsonAsync(session, RelOf(topic.GetProperty("lessons")[0], "lesson"));
        var exercise = lesson.GetProperty("exercises")[0];
        var href = RelOf(exercise, "answer");
        var correct = await AlternativeAsync(exercise.GetProperty("id").GetGuid(), correct: true);

        // Act
        var response = await PostWithTokenAsync(href, new { alternativeId = correct }, AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "Um tópico bloqueado responde 403 com student.topic.locked, e nada é gravado")]
    public async Task Attempt_OnALockedTopic_Returns403AndWritesNothing()
    {
        // Arrange
        var (session, trail, _) = await StudentWithAnExerciseAsync();
        var topic = await GetJsonAsync(session, $"/topics/{IdOf(trail, "fracoes")}");
        var lessonId = topic.GetProperty("lessons")[0].GetProperty("id").GetGuid();
        var lockedExercise = await ScalarAsync<Guid>($"SELECT id FROM exercise WHERE lesson_id = '{lessonId}' ORDER BY position LIMIT 1");
        var alternative = await AlternativeAsync(lockedExercise, correct: true);

        // Act
        var response = await AnswerAsync(session, lockedExercise, alternative);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "student.topic.locked");
        Assert.Equal(0, await CountAsync("attempt"));
        Assert.Equal(0, await CountAsync("student_flashcard"));
    }

    [Fact(DisplayName = "Uma alternativa de outro exercício responde 400 com answer.alternative.notInExercise, e nada é gravado")]
    public async Task Attempt_WithAnAlternativeOfAnotherExercise_Returns400()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();
        var foreign = await ScalarAsync<Guid>($"SELECT id FROM alternative WHERE exercise_id <> '{exerciseId}' LIMIT 1");

        // Act
        var response = await AnswerAsync(session, exerciseId, foreign);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "answer.alternative.notInExercise");
        Assert.Equal(0, await CountAsync("attempt"));
    }

    [Fact(DisplayName = "Sem alternativeId no corpo, o erro é o mesmo de qualquer alternativa que não é do exercício")]
    public async Task Attempt_WithoutAnAlternative_Returns400WithTheSameCode()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();

        // Act
        var response = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { }, AccessTokenOf(session));
        var nullAlternative = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId = (Guid?)null }, AccessTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "answer.alternative.notInExercise");
        await AssertProblemAsync(nullAlternative, HttpStatusCode.BadRequest, "answer.alternative.notInExercise");
    }

    [Fact(DisplayName = "Um tempo de resposta negativo responde 400 com attempt.responseTime.invalid, e nada é gravado")]
    public async Task Attempt_WithANegativeResponseTime_Returns400()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();

        // Act
        var response = await AnswerAsync(session, exerciseId, await AlternativeAsync(exerciseId, correct: true), seconds: -5);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "attempt.responseTime.invalid");
        Assert.Equal(0, await CountAsync("attempt"));
    }

    [Fact(DisplayName = "O tempo de resposta é opcional e pode ser zero")]
    public async Task Attempt_TheResponseTimeIsOptional()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();
        var correct = await AlternativeAsync(exerciseId, correct: true);

        // Act
        var withoutTime = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId = correct }, AccessTokenOf(session));
        var zero = await AnswerAsync(session, exerciseId, correct, seconds: 0);

        // Assert
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (withoutTime.StatusCode, zero.StatusCode));
        Assert.Equal(1, await CountAsync("attempt", "response_seconds IS NULL"));
        Assert.Equal(1, await CountAsync("attempt", "response_seconds = 0"));
    }

    [Fact(DisplayName = "Um exercício que não existe responde 404 com trail.exercise.notFound")]
    public async Task Attempt_ForAnUnknownExercise_Returns404()
    {
        // Arrange
        var (session, _, _) = await StudentWithAnExerciseAsync();

        // Act
        var response = await AnswerAsync(session, Guid.CreateVersion7(), Guid.CreateVersion7());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "trail.exercise.notFound");
    }

    [Fact(DisplayName = "JSON malformado responde 400 e corpo que não é JSON responde 415, ambos sem code")]
    public async Task Attempt_WithABadBody_Returns400Or415()
    {
        // Arrange
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();

        async Task<HttpResponseMessage> PostRaw(string body, string contentType)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/exercises/{exerciseId}/attempts")
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessTokenOf(session));
            return await Client.SendAsync(request, Cancel);
        }

        // Act
        var malformed = await PostRaw("{ not json", "application/json");
        var notJson = await PostRaw("alternativeId=1", "text/plain");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJson.StatusCode);
        Assert.Equal(0, await CountAsync("attempt"));
    }

    [Fact(DisplayName = "Sem token responder responde 401 com o desafio Bearer")]
    public async Task Attempt_WithoutToken_Returns401()
    {
        // Act
        var response = await PostAsync($"/exercises/{Guid.CreateVersion7()}/attempts", new { alternativeId = Guid.CreateVersion7() });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "Um token válido de uma conta que não existe responde 401 com identity.session.invalid")]
    public async Task Attempt_ForAnUnknownAccount_Returns401WithTheSessionCode()
    {
        // Act
        var response = await PostWithTokenAsync(
            $"/exercises/{Guid.CreateVersion7()}/attempts", new { alternativeId = Guid.CreateVersion7() }, CraftedTokens.Create(userId: Guid.CreateVersion7()));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
    }

    [Fact(DisplayName = "Cada aluno responde por si: a tentativa de um não aparece no progresso do outro")]
    public async Task Attempt_WithTwoStudents_KeepsEachOwnProgress()
    {
        // Arrange
        var (ana, _, exerciseId) = await StudentWithAnExerciseAsync("ana@example.com");
        var bia = await RegisterAsync("bia@example.com", "Bia");

        // Act
        await AnswerOkAsync(ana, exerciseId, correct: false);
        var biaTopic = await GetJsonAsync(bia, $"/topics/{IdOf(await TrailAsync(bia), "operacoes-basicas")}");

        // Assert
        Assert.Equal(0, biaTopic.GetProperty("attemptCount").GetInt32());
        Assert.Equal(1, await CountAsync("attempt", $"student_id = '{StudentIdOf(ana)}'"));
        Assert.Equal(0, await CountAsync("attempt", $"student_id = '{StudentIdOf(bia)}'"));
    }

    [Fact(DisplayName = "RNF-RSP-01: responder leva menos de 2 segundos")]
    public async Task Attempt_RespondsWithinTwoSeconds()
    {
        // Arrange: uma resposta antes, para o servidor já estar aquecido (a primeira paga a compilação das consultas)
        var (session, _, exerciseId) = await StudentWithAnExerciseAsync();
        await AnswerOkAsync(session, exerciseId, correct: true);
        var wrong = await AlternativeAsync(exerciseId, correct: false);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await AnswerAsync(session, exerciseId, wrong);
        stopwatch.Stop();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"A resposta levou {stopwatch.ElapsedMilliseconds} ms.");
    }
}
