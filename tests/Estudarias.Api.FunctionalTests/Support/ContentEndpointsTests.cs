using System.Text.Json;
using Estudarias.Application.Content;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Api.FunctionalTests.Support;

/// <summary>
/// Base dos testes dos endpoints que leem o conteúdo (trilha, tópico, lição): a API inteira por HTTP, com o conteúdo de verdade (a pasta
/// <c>content</c> do repositório) carregado pelo mesmo caminho do comando <c>seed</c>.
/// </summary>
public abstract class ContentEndpointsTests(PostgresFixture postgres) : AuthEndpointsTests(postgres)
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

    protected async Task LoadContentAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IContentLoader>().LoadAsync(ContentFolder(), Cancel);
        Assert.True(result.Succeeded, string.Join('\n', result.Problems));
    }

    protected async Task<JsonElement> GetJsonAsync(JsonElement session, string path) =>
        await JsonAsync(await GetWithTokenAsync(path, AccessTokenOf(session)));

    protected async Task<JsonElement> TrailAsync(JsonElement session) => await GetJsonAsync(session, "/trail");

    /// <summary>O item da trilha com o slug, que é estável entre cargas (o id muda de banco para banco).</summary>
    protected static JsonElement TopicOf(JsonElement trail, string slug) =>
        trail.GetProperty("topics").EnumerateArray().Single(topic => topic.GetProperty("slug").GetString() == slug);

    protected static Guid IdOf(JsonElement trail, string slug) => TopicOf(trail, slug).GetProperty("id").GetGuid();

    protected static string RelOf(JsonElement resource, string rel) =>
        resource.GetProperty("links").EnumerateArray()
            .Single(link => link.GetProperty("rel").GetString() == rel).GetProperty("href").GetString()!;

    /// <summary>Libera o tópico para o aluno e, se pedido, o marca como dominado: o estado que o app só alcança respondendo exercícios.</summary>
    protected async Task GrantAsync(JsonElement session, Guid topicId, bool mastered = false)
    {
        var studentId = session.GetProperty("user").GetProperty("studentId").GetGuid();
        await ExecuteAsync(
            "INSERT INTO student_topic (id, student_id, topic_id, unlocked_at, mastered_at) " +
            $"VALUES ('{Guid.CreateVersion7()}', '{studentId}', '{topicId}', now(), {(mastered ? "now()" : "NULL")}) " +
            "ON CONFLICT (student_id, topic_id) DO UPDATE SET mastered_at = EXCLUDED.mastered_at");
    }
}
