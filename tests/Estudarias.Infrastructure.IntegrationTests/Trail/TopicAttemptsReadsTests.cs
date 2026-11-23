using Estudarias.Application.Trail;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Attempts;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Trail;

/// <summary>O histórico de respostas do tópico (#92) contra um PostgreSQL real: é exatamente a janela que compõe a nota, na ordem dela.</summary>
[Collection(PostgresCollection.Name)]
public class TopicAttemptsReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<IReadOnlyList<TopicAttemptView>> HistoryAsync(Guid userId, Guid topicId)
    {
        using var scope = NewScope();
        return await scope.Get<ITopicAttemptsReads>().FindAsync(userId, topicId, Cancel);
    }

    private async Task<(SampleContent Content, Guid UserId, Guid StudentId)> StudentInFractionsAsync(string email = "ana@example.com")
    {
        var content = await SeedContentAsync();
        var user = await SeedUserAsync(email);
        await this.UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        return (content, user.Id, user.StudentId!.Value);
    }

    [Fact(DisplayName = "As respostas vêm da mais recente para a mais antiga, com o exercício, a dificuldade e se acertou")]
    public async Task History_ReturnsTheAnswersMostRecentFirst()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();
        var exercises = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy, Difficulty.Hard);
        await this.AnswerAsync(studentId, exercises[0], correct: false);
        await this.AnswerAsync(studentId, exercises[1], correct: true);

        // Act
        var history = await HistoryAsync(userId, content.Fractions.Id);

        // Assert
        Assert.Equal([exercises[1].Id, exercises[0].Id], history.Select(item => item.ExerciseId));
        Assert.Equal([true, false], history.Select(item => item.IsCorrect));
        Assert.Equal([Difficulty.Hard, Difficulty.Easy], history.Select(item => item.Difficulty));
        Assert.Equal(["Pergunta 1", "Pergunta 0"], history.Select(item => item.Statement));
        Assert.True(history[0].AnsweredAt > history[1].AnsweredAt);
    }

    [Fact(DisplayName = "Só as 10 respostas mais recentes aparecem, as mais antigas ficam de fora")]
    public async Task History_ReturnsOnlyTheScoreWindow()
    {
        // Arrange: 12 respostas, da primeira (errada) à última
        var (content, userId, studentId) = await StudentInFractionsAsync();
        var exercises = await this.SeedExercisesAsync(content, content.Fractions.Id, Enumerable.Repeat(Difficulty.Easy, 12).ToArray());
        foreach (var exercise in exercises)
        {
            await this.AnswerAsync(studentId, exercise, correct: exercise != exercises[0]);
        }

        // Act
        var history = await HistoryAsync(userId, content.Fractions.Id);

        // Assert
        Assert.Equal(TopicScore.Window, history.Count);
        Assert.Equal(exercises.Skip(2).Reverse().Select(exercise => exercise.Id), history.Select(item => item.ExerciseId));
        Assert.DoesNotContain(history, item => item.ExerciseId == exercises[0].Id || item.ExerciseId == exercises[1].Id);
    }

    [Fact(DisplayName = "RN-TRL-17: o histórico é a janela da nota: refazer a nota por ele dá a nota do tópico")]
    public async Task History_RebuildsTheTopicScore()
    {
        // Arrange: respostas de dificuldades e resultados variados, mais de 10
        var (content, userId, studentId) = await StudentInFractionsAsync();
        Difficulty[] difficulties = [Difficulty.Easy, Difficulty.Hard, Difficulty.Medium, Difficulty.Easy, Difficulty.Hard, Difficulty.Medium, Difficulty.Easy, Difficulty.Hard, Difficulty.Medium, Difficulty.Easy, Difficulty.Hard, Difficulty.Medium, Difficulty.Easy];
        var exercises = await this.SeedExercisesAsync(content, content.Fractions.Id, difficulties);
        for (var index = 0; index < exercises.Count; index++)
        {
            await this.AnswerAsync(studentId, exercises[index], correct: index % 3 != 0);
        }

        // Act
        var history = await HistoryAsync(userId, content.Fractions.Id);
        using var scope = NewScope();
        var topic = await scope.Get<ITopicReads>().FindAsync(userId, content.Fractions.Id, Cancel);

        // Assert
        var rebuilt = TopicScore.Compute([.. history.Select(item => new GradedAnswer(item.Difficulty, item.IsCorrect))]).Display;
        Assert.Equal(topic.Score, rebuilt);
        Assert.Equal(13, topic.AttemptCount);
        Assert.Equal(TopicScore.Window, history.Count);
    }

    [Fact(DisplayName = "Duas respostas no mesmo instante saem sempre na mesma ordem: a do id, da maior para a menor, como na nota")]
    public async Task History_BreaksTiesByTheAttemptId()
    {
        // Arrange: duas tentativas no mesmo instante, sem avançar o relógio
        var (content, userId, studentId) = await StudentInFractionsAsync();
        var exercises = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy, Difficulty.Easy);
        using (var scope = NewScope())
        {
            foreach (var exercise in exercises)
            {
                scope.Attempts.Add(Attempt.Create(studentId, exercise.Id, exercise.Alternatives.First().Id, null, Clock));
            }

            await scope.UnitOfWork.SaveChangesAsync(Cancel);
        }

        // Act
        var history = await HistoryAsync(userId, content.Fractions.Id);

        // Assert
        Assert.Equal(2, history.Count);
        Assert.Equal(history[0].AnsweredAt, history[1].AnsweredAt);
        Assert.Equal(history.Select(item => item.AttemptId).OrderDescending(), history.Select(item => item.AttemptId));
    }

    [Fact(DisplayName = "Um tópico liberado sem respostas devolve a lista vazia")]
    public async Task History_WithoutAnswers_IsEmpty()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync();

        // Act
        var history = await HistoryAsync(userId, content.Fractions.Id);

        // Assert
        Assert.Empty(history);
    }

    [Fact(DisplayName = "As respostas de outro aluno e as de outro tópico não aparecem")]
    public async Task History_IgnoresTheAnswersOfAnotherStudentAndOfAnotherTopic()
    {
        // Arrange
        var (content, anaId, anaStudent) = await StudentInFractionsAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        await this.UpdateStudentAsync(bia.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        var fractions = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy);
        var basics = await this.SeedExercisesAsync(content, content.Basics.Id, Difficulty.Easy);
        await this.AnswerAsync(bia.StudentId!.Value, fractions[0], correct: true);
        await this.AnswerAsync(anaStudent, basics[0], correct: true);

        // Act
        var history = await HistoryAsync(anaId, content.Fractions.Id);

        // Assert
        Assert.Empty(history);
    }

    [Fact(DisplayName = "O histórico mostra o enunciado e o resultado, mas não a alternativa escolhida nem a correta")]
    public void View_HasNoAnswerKeyField()
    {
        // Assert
        Assert.Equal(
            ["AnsweredAt", "AttemptId", "Difficulty", "ExerciseId", "IsCorrect", "Statement"],
            typeof(TopicAttemptView).GetProperties().Select(property => property.Name).Order());
    }

    [Fact(DisplayName = "Um tópico bloqueado é student.topic.locked (403)")]
    public async Task History_ForALockedTopic_ThrowsForbidden()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainForbiddenException>(() => HistoryAsync(user.Id, content.Fractions.Id));

        // Assert
        Assert.Equal(StudentErrors.TopicLocked, exception.Code);
    }

    [Fact(DisplayName = "Um tópico que não existe é trail.topic.notFound (404)")]
    public async Task History_ForAnUnknownTopic_ThrowsNotFound()
    {
        // Arrange
        await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() => HistoryAsync(user.Id, Guid.CreateVersion7()));

        // Assert
        Assert.Equal(TrailErrors.TopicNotFound, exception.Code);
    }

    [Fact(DisplayName = "Uma conta que não existe ou está desabilitada é sessão inválida")]
    public async Task History_ForAnInvalidAccount_ThrowsInvalidSession()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await ExecuteAsync($"UPDATE app_user SET is_active = false WHERE id = '{user.Id}'");

        // Act & Assert
        foreach (var userId in new[] { Guid.CreateVersion7(), user.Id })
        {
            var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => HistoryAsync(userId, content.Fractions.Id));
            Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
        }
    }
}
