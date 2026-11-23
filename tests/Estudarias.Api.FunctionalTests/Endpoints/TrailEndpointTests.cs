using System.Net;
using System.Text.Json;
using Estudarias.Api.FunctionalTests.Support;
using Estudarias.Application.Content;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Api.FunctionalTests.Endpoints;

/// <summary>
/// GET /trail por HTTP contra um PostgreSQL real, com o conteúdo de verdade (a pasta <c>content</c>): a trilha de quem está logado,
/// a liberação inicial e os links. O token vem do cadastro, como o app o obteria.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TrailEndpointTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
{
    private static string ContentFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Estudarias.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Solution root not found."), "content");
    }

    private async Task LoadContentAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IContentLoader>().LoadAsync(ContentFolder(), Cancel);
        Assert.True(result.Succeeded, string.Join('\n', result.Problems));
    }

    private async Task<JsonElement> TrailAsync(JsonElement session) =>
        await JsonAsync(await GetWithTokenAsync("/trail", AccessTokenOf(session)));

    private static JsonElement TopicOf(JsonElement trail, string slug) =>
        trail.GetProperty("topics").EnumerateArray().Single(topic => topic.GetProperty("slug").GetString() == slug);

    [Fact(DisplayName = "GET /trail responde 200 com a raiz disponível, os dependentes bloqueados e os totais")]
    public async Task Trail_ForANewStudent_ShowsTheRootAvailable()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();

        // Act
        var response = await GetWithTokenAsync("/trail", AccessTokenOf(session));
        var trail = await JsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(70, trail.GetProperty("masteryThreshold").GetDecimal());
        Assert.Equal(0, trail.GetProperty("masteredTopics").GetInt32());
        Assert.Equal(0, trail.GetProperty("dueReviews").GetInt32());
        Assert.Equal(trail.GetProperty("topics").GetArrayLength(), trail.GetProperty("totalTopics").GetInt32());
        Assert.Equal("available", TopicOf(trail, "operacoes-basicas").GetProperty("status").GetString());
        Assert.Equal("locked", TopicOf(trail, "fracoes").GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Os tópicos vêm em ordem de pré-requisito, cada um com os ids dos pré-requisitos e a nota nula")]
    public async Task Trail_ListsTheTopicsInPrerequisiteOrder()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();

        // Act
        var trail = await TrailAsync(session);

        // Assert
        var topics = trail.GetProperty("topics").EnumerateArray().ToList();
        var slugs = topics.Select(topic => topic.GetProperty("slug").GetString()).ToList();
        Assert.True(slugs.IndexOf("operacoes-basicas") < slugs.IndexOf("fracoes"));
        var basics = TopicOf(trail, "operacoes-basicas");
        var fractions = TopicOf(trail, "fracoes");
        Assert.Equal(0, basics.GetProperty("prerequisites").GetArrayLength());
        Assert.Equal(
            [basics.GetProperty("id").GetGuid()],
            fractions.GetProperty("prerequisites").EnumerateArray().Select(id => id.GetGuid()));
        Assert.All(topics, topic => Assert.Equal(JsonValueKind.Null, topic.GetProperty("score").ValueKind));
    }

    [Fact(DisplayName = "A trilha tem só os campos do contrato, e o status é texto em minúsculas")]
    public async Task Trail_ReturnsOnlyTheContractFields()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();

        // Act
        var trail = await TrailAsync(session);

        // Assert
        Assert.Equal(
            ["dueReviews", "links", "masteredTopics", "masteryThreshold", "topics", "totalTopics"],
            trail.EnumerateObject().Select(property => property.Name).Order());
        var topic = trail.GetProperty("topics")[0];
        Assert.Equal(
            ["id", "links", "name", "prerequisites", "score", "slug", "status"],
            topic.EnumerateObject().Select(property => property.Name).Order());
        Assert.Contains(topic.GetProperty("status").GetString(), new[] { "locked", "available", "mastered" });
    }

    [Fact(DisplayName = "A trilha traz o link self, com a URL absoluta da própria trilha")]
    public async Task Trail_LinksToItself()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();

        // Act
        var trail = await TrailAsync(session);

        // Assert
        var link = trail.GetProperty("links").EnumerateArray().Single(candidate => candidate.GetProperty("rel").GetString() == "self");
        Assert.Equal(["href", "method", "rel"], link.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("GET", link.GetProperty("method").GetString());
        Assert.Equal("http://localhost/trail", link.GetProperty("href").GetString());
    }

    [Fact(DisplayName = "O link trail do perfil leva à trilha")]
    public async Task Trail_IsReachableFromTheProfileLink()
    {
        // Arrange
        await LoadContentAsync();
        var session = await RegisterAsync();
        var me = await JsonAsync(await GetWithTokenAsync("/me", AccessTokenOf(session)));
        var href = me.GetProperty("links").EnumerateArray()
            .Single(link => link.GetProperty("rel").GetString() == "trail").GetProperty("href").GetString()!;

        // Act
        var response = await GetWithTokenAsync(href, AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "O cadastro depois da carga do conteúdo já libera a raiz, antes de abrir a trilha")]
    public async Task Register_AfterTheContent_UnlocksTheRootBeforeTheTrailIsOpened()
    {
        // Arrange
        await LoadContentAsync();

        // Act
        await RegisterAsync();

        // Assert
        Assert.Equal(1, await CountAsync("student_topic"));
    }

    [Fact(DisplayName = "Quem se cadastrou antes de existir conteúdo tem a raiz liberada ao abrir a trilha")]
    public async Task Trail_ForAStudentRegisteredBeforeTheContent_UnlocksTheRoot()
    {
        // Arrange
        var session = await RegisterAsync();
        await LoadContentAsync();
        Assert.Equal(0, await CountAsync("student_topic"));

        // Act
        var trail = await TrailAsync(session);

        // Assert
        Assert.Equal("available", TopicOf(trail, "operacoes-basicas").GetProperty("status").GetString());
        Assert.Equal(1, await CountAsync("student_topic"));
    }

    [Fact(DisplayName = "Abrir a trilha de novo não duplica a liberação nem muda a resposta")]
    public async Task Trail_OpenedTwice_IsIdempotent()
    {
        // Arrange
        var session = await RegisterAsync();
        await LoadContentAsync();

        // Act
        var first = await TrailAsync(session);
        var second = await TrailAsync(session);

        // Assert
        Assert.Equal(1, await CountAsync("student_topic"));
        Assert.Equal(first.GetRawText(), second.GetRawText());
    }

    [Fact(DisplayName = "Cada aluno vê o próprio progresso, e não o de outro")]
    public async Task Trail_WithTwoStudents_ShowsEachOwnProgress()
    {
        // Arrange
        await LoadContentAsync();
        var ana = await RegisterAsync("ana@example.com", "Ana");
        var bia = await RegisterAsync("bia@example.com", "Bia");
        var anaStudentId = ana.GetProperty("user").GetProperty("studentId").GetGuid();
        await ExecuteAsync($"UPDATE student_topic SET mastered_at = unlocked_at WHERE student_id = '{anaStudentId}'");

        // Act
        var anaTrail = await TrailAsync(ana);
        var biaTrail = await TrailAsync(bia);

        // Assert
        Assert.Equal("mastered", TopicOf(anaTrail, "operacoes-basicas").GetProperty("status").GetString());
        Assert.Equal(1, anaTrail.GetProperty("masteredTopics").GetInt32());
        Assert.Equal("available", TopicOf(biaTrail, "operacoes-basicas").GetProperty("status").GetString());
        Assert.Equal(0, biaTrail.GetProperty("masteredTopics").GetInt32());
    }

    [Fact(DisplayName = "Sem conteúdo carregado, a trilha é vazia")]
    public async Task Trail_WithoutContent_IsEmpty()
    {
        // Arrange
        var session = await RegisterAsync();

        // Act
        var trail = await TrailAsync(session);

        // Assert
        Assert.Equal(0, trail.GetProperty("totalTopics").GetInt32());
        Assert.Equal(0, trail.GetProperty("topics").GetArrayLength());
    }

    [Fact(DisplayName = "Sem token a trilha responde 401 com o desafio Bearer")]
    public async Task Trail_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.GetAsync("/trail", Cancel);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "Um token válido de uma conta que não existe responde 401 com identity.session.invalid")]
    public async Task Trail_ForAnUnknownAccount_Returns401WithTheSessionCode()
    {
        // Arrange
        var token = CraftedTokens.Create(userId: Guid.CreateVersion7());

        // Act
        var response = await GetWithTokenAsync("/trail", token);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "O token de uma conta desabilitada depois do login responde 401 com identity.session.invalid, sem gravar nada")]
    public async Task Trail_ForADisabledAccount_Returns401AndWritesNothing()
    {
        // Arrange
        var session = await RegisterAsync();
        await LoadContentAsync();
        await ExecuteAsync("UPDATE app_user SET is_active = false");

        // Act
        var response = await GetWithTokenAsync("/trail", AccessTokenOf(session));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "identity.session.invalid");
        Assert.Equal(0, await CountAsync("student_topic"));
    }
}
