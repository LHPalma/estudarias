using System.Net;
using System.Text;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// GET /reviews/due e POST /reviews/{id} por HTTP contra um PostgreSQL real, com o conteúdo de verdade: os cartões nascem de erros de
/// verdade (POST /exercises/{id}/attempts), e o tempo é avançado no banco. O token vem do cadastro, como o app o obteria.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ReviewEndpointTests(PostgresFixture postgres) : ContentEndpointsTests(postgres)
{
    private static Guid StudentIdOf(JsonElement session) => session.GetProperty("user").GetProperty("studentId").GetGuid();

    private async Task<(JsonElement Session, Guid ExerciseId)> StudentWithAnExerciseAsync(string email = "ana@example.com")
    {
        var session = await RegisterAsync(email);
        var trail = await TrailAsync(session);
        var topic = await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}");
        var lesson = await GetJsonAsync(session, RelOf(topic.GetProperty("lessons")[0], "lesson"));
        return (session, lesson.GetProperty("exercises")[0].GetProperty("id").GetGuid());
    }

    /// <summary>Erra o exercício: nasce o cartão do tipo de erro. Devolve o id do cartão do aluno.</summary>
    private async Task<Guid> ErrAsync(JsonElement session, Guid exerciseId)
    {
        var wrong = await ScalarAsync<Guid>($"SELECT id FROM alternative WHERE exercise_id = '{exerciseId}' AND NOT is_correct ORDER BY position LIMIT 1");
        var response = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId = wrong }, AccessTokenOf(session));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = (await JsonAsync(response)).GetProperty("flashcard");
        return card.GetProperty("id").GetGuid();
    }

    /// <summary>Faz o cartão vencer: recua a próxima revisão no banco (o servidor usa o relógio real).</summary>
    private Task MakeDueAsync(Guid cardId, int daysOverdue = 2) =>
        ExecuteAsync($"UPDATE student_flashcard SET next_review_at = now() - interval '{daysOverdue} days' WHERE id = '{cardId}'");

    private Task<JsonElement> DueAsync(JsonElement session) => GetJsonAsync(session, "/reviews/due");

    private Task<HttpResponseMessage> ReviewAsync(JsonElement session, Guid cardId, object? body) =>
        PostWithTokenAsync($"/reviews/{cardId}", body, AccessTokenOf(session));

    [Fact(DisplayName = "GET /reviews/due responde 200 com o cartão vencido, a pergunta, a resposta, o tópico e o link review")]
    public async Task Due_ReturnsTheDueCard()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);
        await MakeDueAsync(cardId);

        // Act
        var response = await GetWithTokenAsync("/reviews/due", AccessTokenOf(session));
        var due = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, due.GetProperty("count").GetInt32());
        var item = Assert.Single(due.GetProperty("items").EnumerateArray());
        Assert.Equal(cardId, item.GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("question").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("answer").GetString()));
        Assert.Equal("Operações básicas", item.GetProperty("topic").GetProperty("name").GetString());
        var link = Assert.Single(item.GetProperty("links").EnumerateArray());
        Assert.Equal(("review", "POST"), (link.GetProperty("rel").GetString(), link.GetProperty("method").GetString()));
        Assert.Equal($"http://localhost/reviews/{cardId}", link.GetProperty("href").GetString());
    }

    [Fact(DisplayName = "A lista tem só os campos do contrato, e os links self e trail")]
    public async Task Due_ReturnsOnlyTheContractFields()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        await MakeDueAsync(await ErrAsync(session, exerciseId));

        // Act
        var due = await DueAsync(session);

        // Assert
        Assert.Equal(["count", "items", "links"], due.EnumerateObject().Select(property => property.Name).Order());
        var item = due.GetProperty("items")[0];
        Assert.Equal(["answer", "id", "links", "nextReviewAt", "question", "topic"], item.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["id", "name"], item.GetProperty("topic").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["self", "trail"], due.GetProperty("links").EnumerateArray().Select(link => link.GetProperty("rel").GetString()));
        Assert.Equal("http://localhost/reviews/due", RelOf(due, "self"));
        Assert.Equal("http://localhost/trail", RelOf(due, "trail"));
    }

    [Fact(DisplayName = "FA-REV-01: sem nenhum cartão, 200 com count 0 e a lista vazia")]
    public async Task Due_WithoutCards_IsEmpty()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();

        // Act
        var due = await DueAsync(session);

        // Assert
        Assert.Equal(0, due.GetProperty("count").GetInt32());
        Assert.Equal(0, due.GetProperty("items").GetArrayLength());
    }

    [Fact(DisplayName = "O cartão que ainda não venceu não aparece na lista")]
    public async Task Due_ExcludesTheCardsNotYetDue()
    {
        // Arrange: o cartão nasce para vencer em um dia
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        await ErrAsync(session, exerciseId);

        // Act
        var due = await DueAsync(session);

        // Assert
        Assert.Equal(0, due.GetProperty("count").GetInt32());
    }

    [Fact(DisplayName = "Os cartões vêm do mais atrasado ao mais recente")]
    public async Task Due_OrdersTheMostOverdueFirst()
    {
        // Arrange: dois tipos de erro diferentes dão dois cartões
        await LoadContentAsync();
        var (session, _) = await StudentWithAnExerciseAsync();
        var cards = new List<Guid>();
        var wrongs = await ScalarAsync<long>("SELECT count(DISTINCT error_type_id) FROM alternative WHERE NOT is_correct");
        Assert.True(wrongs >= 2);
        foreach (var wrong in await WrongAlternativesOfDifferentErrorTypesAsync(2))
        {
            var exerciseId = await ScalarAsync<Guid>($"SELECT exercise_id FROM alternative WHERE id = '{wrong}'");
            await GrantTopicOfAsync(session, exerciseId);
            var response = await PostWithTokenAsync($"/exercises/{exerciseId}/attempts", new { alternativeId = wrong }, AccessTokenOf(session));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            cards.Add((await JsonAsync(response)).GetProperty("flashcard").GetProperty("id").GetGuid());
        }

        await MakeDueAsync(cards[0], daysOverdue: 1);
        await MakeDueAsync(cards[1], daysOverdue: 5);

        // Act
        var due = await DueAsync(session);

        // Assert
        Assert.Equal([cards[1], cards[0]], due.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    private async Task<List<Guid>> WrongAlternativesOfDifferentErrorTypesAsync(int count)
    {
        var ids = new List<Guid>();
        await using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT DISTINCT ON (error_type_id) id FROM alternative WHERE NOT is_correct ORDER BY error_type_id, position LIMIT " + count, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancel);
        while (await reader.ReadAsync(Cancel))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private Task GrantTopicOfAsync(JsonElement session, Guid exerciseId) => ExecuteAsync(
        "INSERT INTO student_topic (id, student_id, topic_id, unlocked_at) " +
        $"SELECT gen_random_uuid(), '{StudentIdOf(session)}', l.topic_id, now() FROM exercise e JOIN lesson l ON l.id = e.lesson_id WHERE e.id = '{exerciseId}' " +
        "ON CONFLICT (student_id, topic_id) DO NOTHING");

    [Fact(DisplayName = "Os cartões de outro aluno não aparecem")]
    public async Task Due_ExcludesTheCardsOfAnotherStudent()
    {
        // Arrange
        await LoadContentAsync();
        var (ana, exerciseId) = await StudentWithAnExerciseAsync("ana@example.com");
        var bia = await RegisterAsync("bia@example.com", "Bia");
        await MakeDueAsync(await ErrAsync(ana, exerciseId));

        // Act
        var biaDue = await DueAsync(bia);
        var anaDue = await DueAsync(ana);

        // Assert
        Assert.Equal(0, biaDue.GetProperty("count").GetInt32());
        Assert.Equal(1, anaDue.GetProperty("count").GetInt32());
    }

    [Fact(DisplayName = "POST /reviews/{id} responde 200 com a próxima revisão e o link reviews, e reagenda o cartão")]
    public async Task Review_ReschedulesTheCard()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);
        await MakeDueAsync(cardId);
        var reviewedAt = Factory.Clock.GetUtcNow();

        // Act
        var response = await ReviewAsync(session, cardId, new { rating = "good" });
        var result = await JsonAsync(response);

        // Assert: a primeira revisão correta do SM-2 leva a 1 dia, contado do instante da revisão (o relógio do servidor, nos testes, é controlado)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["id", "links", "nextReviewAt"], result.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(cardId, result.GetProperty("id").GetGuid());
        Assert.Equal(reviewedAt.AddDays(1), result.GetProperty("nextReviewAt").GetDateTimeOffset());
        var link = Assert.Single(result.GetProperty("links").EnumerateArray());
        Assert.Equal(("reviews", "GET", "http://localhost/reviews/due"), (link.GetProperty("rel").GetString(), link.GetProperty("method").GetString(), link.GetProperty("href").GetString()));
    }

    [Fact(DisplayName = "Depois de revisar, o cartão sai da lista, e a trilha mostra uma revisão a menos")]
    public async Task Review_RemovesTheCardFromTheListAndTheTrailCount()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);
        await MakeDueAsync(cardId);
        Assert.Equal(1, (await TrailAsync(session)).GetProperty("dueReviews").GetInt32());

        // Act
        await ReviewAsync(session, cardId, new { rating = "easy" });

        // Assert
        Assert.Equal(0, (await DueAsync(session)).GetProperty("count").GetInt32());
        Assert.Equal(0, (await TrailAsync(session)).GetProperty("dueReviews").GetInt32());
    }

    [Fact(DisplayName = "O link review da lista leva a este endpoint, e as quatro notas são aceitas")]
    public async Task Review_IsReachableFromTheDueListAndAcceptsEachRating()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);

        foreach (var rating in new[] { "again", "hard", "good", "easy" })
        {
            await MakeDueAsync(cardId);
            var href = RelOf((await DueAsync(session)).GetProperty("items")[0], "review");

            // Act
            var response = await PostWithTokenAsync(href, new { rating }, AccessTokenOf(session));

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact(DisplayName = "FA-REV-02: revisar antes do vencimento é aceito")]
    public async Task Review_BeforeTheDueDate_IsAccepted()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);

        // Act
        var response = await ReviewAsync(session, cardId, new { rating = "good" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "RN-REV-18: revisar não muda a nota nem o status do tópico e não grava tentativa")]
    public async Task Review_DoesNotChangeTheScoreOrTheTopic()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);
        await MakeDueAsync(cardId);
        var trail = await TrailAsync(session);
        var topicBefore = (await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}")).GetRawText();
        var attempts = await CountAsync("attempt");

        // Act
        await ReviewAsync(session, cardId, new { rating = "good" });

        // Assert
        Assert.Equal(topicBefore, (await GetJsonAsync(session, $"/topics/{IdOf(trail, "operacoes-basicas")}")).GetRawText());
        Assert.Equal(attempts, await CountAsync("attempt"));
    }

    [Fact(DisplayName = "RN-REV-11: o cartão de outro aluno responde 404 com review.card.notFound, igual a um que não existe, e nada muda")]
    public async Task Review_TheCardOfAnotherStudent_Returns404()
    {
        // Arrange
        await LoadContentAsync();
        var (ana, exerciseId) = await StudentWithAnExerciseAsync("ana@example.com");
        var bia = await RegisterAsync("bia@example.com", "Bia");
        var cardId = await ErrAsync(ana, exerciseId);
        var stateBefore = await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{cardId}'");

        // Act
        var other = await ReviewAsync(bia, cardId, new { rating = "good" });
        var unknown = await ReviewAsync(bia, Guid.CreateVersion7(), new { rating = "good" });

        // Assert
        var otherProblem = await AssertProblemAsync(other, HttpStatusCode.NotFound, "review.card.notFound");
        var unknownProblem = await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "review.card.notFound");
        Assert.Equal(otherProblem.GetProperty("detail").GetString(), unknownProblem.GetProperty("detail").GetString());
        Assert.Equal(stateBefore, await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{cardId}'"));
    }

    [Fact(DisplayName = "FA-REV-03: uma nota desconhecida ou ausente responde 400 com studentFlashcard.rating.invalid, e nada é gravado")]
    public async Task Review_WithAnInvalidRating_Returns400()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);
        var stateBefore = await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{cardId}'");

        // Act
        var unknown = await ReviewAsync(session, cardId, new { rating = "perfect" });
        var uppercase = await ReviewAsync(session, cardId, new { rating = "Good" });
        var missing = await ReviewAsync(session, cardId, new { });
        var nullRating = await ReviewAsync(session, cardId, new { rating = (string?)null });

        // Assert
        foreach (var response in new[] { unknown, uppercase, missing, nullRating })
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "studentFlashcard.rating.invalid");
        }

        Assert.Equal(stateBefore, await ScalarAsync<string>($"SELECT scheduling_state::text FROM student_flashcard WHERE id = '{cardId}'"));
    }

    [Fact(DisplayName = "JSON malformado responde 400 e corpo que não é JSON responde 415, ambos sem code")]
    public async Task Review_WithABadBody_Returns400Or415()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        var cardId = await ErrAsync(session, exerciseId);

        async Task<HttpResponseMessage> PostRaw(string body, string contentType)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/reviews/{cardId}") { Content = new StringContent(body, Encoding.UTF8, contentType) };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessTokenOf(session));
            return await Client.SendAsync(request, Cancel);
        }

        // Act
        var malformed = await PostRaw("{ not json", "application/json");
        var notJson = await PostRaw("rating=good", "text/plain");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJson.StatusCode);
    }

    [Fact(DisplayName = "Sem token as duas rotas respondem 401 com o desafio Bearer")]
    public async Task Reviews_WithoutToken_Return401()
    {
        // Act
        var due = await Client.GetAsync("/reviews/due", Cancel);
        var review = await PostAsync($"/reviews/{Guid.CreateVersion7()}", new { rating = "good" });

        // Assert
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (due.StatusCode, review.StatusCode));
        Assert.Equal("Bearer", due.Headers.WwwAuthenticate.ToString());
        Assert.Equal("Bearer", review.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "Um token válido de uma conta que não existe responde 401 com identity.session.invalid nas duas rotas")]
    public async Task Reviews_ForAnUnknownAccount_Return401WithTheSessionCode()
    {
        // Arrange
        var token = CraftedTokens.Create(userId: Guid.CreateVersion7());

        // Act
        var due = await GetWithTokenAsync("/reviews/due", token);
        var review = await PostWithTokenAsync($"/reviews/{Guid.CreateVersion7()}", new { rating = "good" }, token);

        // Assert
        await AssertProblemAsync(due, HttpStatusCode.Unauthorized, "identity.session.invalid");
        await AssertProblemAsync(review, HttpStatusCode.Unauthorized, "identity.session.invalid");
    }

    [Fact(DisplayName = "A lista é só de leitura: nada é gravado no banco")]
    public async Task Due_WritesNothing()
    {
        // Arrange
        await LoadContentAsync();
        var (session, exerciseId) = await StudentWithAnExerciseAsync();
        await MakeDueAsync(await ErrAsync(session, exerciseId));
        var before = await ScalarAsync<string>("SELECT string_agg(scheduling_state::text || next_review_at::text, ',') FROM student_flashcard");

        // Act
        await DueAsync(session);
        await DueAsync(session);

        // Assert
        Assert.Equal(before, await ScalarAsync<string>("SELECT string_agg(scheduling_state::text || next_review_at::text, ',') FROM student_flashcard"));
    }
}
