using Estudarias.Application.Trail;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Estudarias.Infrastructure.IntegrationTests.Trail;

/// <summary>A leitura do tópico (#87) contra um PostgreSQL real: status, nota, tentativas, pré-requisitos, o que libera e as lições.</summary>
[Collection(PostgresCollection.Name)]
public class TopicReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<TopicView> FindAsync(Guid userId, Guid topicId)
    {
        using var scope = NewScope();
        return await scope.Get<ITopicReads>().FindAsync(userId, topicId, Cancel);
    }

    [Fact(DisplayName = "O tópico disponível traz a nota, as tentativas, a meta, o mínimo, os pré-requisitos com o status e o que libera")]
    public async Task Find_ForAnAvailableTopic_ReturnsEverything()
    {
        // Arrange: Operações básicas dominada, Frações disponível com uma resposta certa
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await this.UpdateStudentAsync(user.StudentId!.Value, student =>
        {
            student.Unlock(content.Basics.Id, Clock);
            student.MarkMastered(content.Basics.Id, Clock);
            student.Unlock(content.Fractions.Id, Clock);
        });
        var exercises = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy);
        await this.AnswerAsync(user.StudentId!.Value, exercises[0], correct: true);

        // Act
        var topic = await FindAsync(user.Id, content.Fractions.Id);

        // Assert
        Assert.Equal((content.Fractions.Id, "fracoes", "Frações", TopicStatus.Available), (topic.Id, topic.Slug, topic.Name, topic.Status));
        Assert.Equal((100.0m, 1), (topic.Score, topic.AttemptCount));
        Assert.Equal((70m, 5), (topic.MasteryThreshold, topic.MinAttempts));
        var prerequisite = Assert.Single(topic.Prerequisites);
        Assert.Equal((content.Basics.Id, "Operações básicas", TopicStatus.Mastered), (prerequisite.Id, prerequisite.Name, prerequisite.Status));
        Assert.Empty(topic.Unlocks);
    }

    [Fact(DisplayName = "O tópico raiz lista o que ele libera, e os pré-requisitos dele são vazios")]
    public async Task Find_ForTheRoot_ListsWhatItUnlocks()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var topic = await FindAsync(user.Id, content.Basics.Id);

        // Assert
        Assert.Empty(topic.Prerequisites);
        var unlocked = Assert.Single(topic.Unlocks);
        Assert.Equal((content.Fractions.Id, "Frações"), (unlocked.Id, unlocked.Name));
    }

    [Fact(DisplayName = "O tópico bloqueado também abre, com o status locked, o que falta nos pré-requisitos e sem nota")]
    public async Task Find_ForALockedTopic_Opens()
    {
        // Arrange: nada liberado, então Frações está bloqueado e a raiz que falta aparece como bloqueada também
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var topic = await FindAsync(user.Id, content.Fractions.Id);

        // Assert
        Assert.Equal(TopicStatus.Locked, topic.Status);
        Assert.Null(topic.Score);
        Assert.Equal(0, topic.AttemptCount);
        Assert.Equal(TopicStatus.Locked, Assert.Single(topic.Prerequisites).Status);
    }

    [Fact(DisplayName = "As tentativas contam todas, e a nota usa só as 10 mais recentes")]
    public async Task Find_AttemptCountIsTheTotalWhileTheScoreUsesTheWindow()
    {
        // Arrange: uma errada, a mais antiga, e depois dez certas: 11 tentativas, nota 100
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await this.UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        var exercises = await this.SeedExercisesAsync(content, content.Fractions.Id, Enumerable.Repeat(Difficulty.Easy, 11).ToArray());
        await this.AnswerAsync(user.StudentId!.Value, exercises[0], correct: false);
        foreach (var exercise in exercises.Skip(1))
        {
            await this.AnswerAsync(user.StudentId!.Value, exercise, correct: true);
        }

        // Act
        var topic = await FindAsync(user.Id, content.Fractions.Id);

        // Assert
        Assert.Equal(11, topic.AttemptCount);
        Assert.Equal(100.0m, topic.Score);
    }

    [Fact(DisplayName = "As respostas de outro aluno e de outro tópico não entram nas tentativas nem na nota")]
    public async Task Find_CountsOnlyTheStudentAndTheTopic()
    {
        // Arrange
        var content = await SeedContentAsync();
        var ana = await SeedUserAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        var fractions = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy);
        var basics = await this.SeedExercisesAsync(content, content.Basics.Id, Difficulty.Easy);
        await this.AnswerAsync(bia.StudentId!.Value, fractions[0], correct: true);
        await this.AnswerAsync(ana.StudentId!.Value, basics[0], correct: true);

        // Act
        var topic = await FindAsync(ana.Id, content.Fractions.Id);

        // Assert
        Assert.Equal(0, topic.AttemptCount);
        Assert.Null(topic.Score);
    }

    [Fact(DisplayName = "As lições vêm por ordem, com o id e o título, e sem o conteúdo")]
    public async Task Find_ListsTheLessonsInOrderWithoutTheContent()
    {
        // Arrange: a lição de exemplo é a 1; uma segunda, na posição 2, criada antes de uma na posição 1 do outro tópico
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var second = Lesson.Create(content.Fractions.Id, 2, "Segunda lição", "Texto da segunda.");
        await WriteAsync(db => db.Lessons.Add(second));

        // Act
        var topic = await FindAsync(user.Id, content.Fractions.Id);

        // Assert
        Assert.Equal([1, 2], topic.Lessons.Select(lesson => lesson.Order));
        Assert.Equal([content.Lesson.Id, second.Id], topic.Lessons.Select(lesson => lesson.Id));
        Assert.Equal(["Soma de frações", "Segunda lição"], topic.Lessons.Select(lesson => lesson.Title));
    }

    [Fact(DisplayName = "A consulta das lições do tópico não lê o conteúdo da lição")]
    public async Task Find_NeverSelectsTheLessonContent()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options;
        await using var context = new AppDbContext(options);

        // Act
        await new TopicReads(context).FindAsync(user.Id, content.Fractions.Id, Cancel);

        // Assert
        Assert.DoesNotContain("content", string.Join('\n', commands.Where(command => command.Contains("FROM lesson", StringComparison.Ordinal))));
    }

    [Fact(DisplayName = "Um tópico que não existe é trail.topic.notFound")]
    public async Task Find_ForAnUnknownTopic_ThrowsNotFound()
    {
        // Arrange
        await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() => FindAsync(user.Id, Guid.CreateVersion7()));

        // Assert
        Assert.Equal(TrailErrors.TopicNotFound, exception.Code);
    }

    [Fact(DisplayName = "Uma conta que não existe, está desabilitada ou não tem aluno é sessão inválida")]
    public async Task Find_ForAnInvalidAccount_ThrowsInvalidSession()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await ExecuteAsync($"UPDATE app_user SET is_active = false WHERE id = '{user.Id}'");

        // Act & Assert
        foreach (var userId in new[] { Guid.CreateVersion7(), user.Id })
        {
            var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => FindAsync(userId, content.Fractions.Id));
            Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
        }
    }
}
