using Estudarias.Application.Attempts;
using Estudarias.Application.StudentFlashcards;
using Estudarias.Application.Topics;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Answers;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Attempts;

/// <summary>
/// Responder um exercício (#88) pelo composition root e contra o PostgreSQL: o que fica gravado (tentativa, progresso, cartão) e a corrida
/// de dois erros do mesmo tipo, fechada pelo índice único do cartão.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SubmitAttemptFlowTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<(SampleContent Content, Guid UserId, Guid StudentId)> StudentInFractionsAsync(
        ExperimentalGroup group = ExperimentalGroup.Diagnostic)
    {
        var content = await SeedContentAsync();
        var user = await SeedUserAsync("ana@example.com", group);
        await this.UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        return (content, user.Id, user.StudentId!.Value);
    }

    private async Task<SubmitAttemptResult> SubmitAsync(Guid userId, Guid exerciseId, Guid alternativeId, int? seconds = 20)
    {
        using var scope = NewScope();
        return await scope.Get<SubmitAttemptHandler>().Handle(userId, new SubmitAttemptCommand(exerciseId, alternativeId, seconds), Cancel);
    }

    [Fact(DisplayName = "Errar grava a tentativa e o cartão com a tentativa de origem, na mesma gravação")]
    public async Task Submit_WithAWrongAlternative_StoresTheAttemptAndTheCard()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();

        // Act
        var result = await SubmitAsync(userId, content.Exercise.Id, content.Wrong.Id);

        // Assert
        Assert.False(result.IsCorrect);
        Assert.Equal(AnswerFeedbackKind.IncorrectWithDiagnosis, result.Feedback.Kind);
        Assert.Equal(1L, await ScalarAsync<long>($"SELECT count(*) FROM attempt WHERE id = '{result.AttemptId}' AND student_id = '{studentId}' AND response_seconds = 20"));
        var card = Assert.IsType<CreatedFlashcardResult>(result.Flashcard);
        Assert.Equal(result.AttemptId, await ScalarAsync<Guid>($"SELECT origin_attempt_id FROM student_flashcard WHERE id = '{card.Id}'"));
        Assert.Equal(content.Generated.Id, await ScalarAsync<Guid>($"SELECT flashcard_id FROM student_flashcard WHERE id = '{card.Id}'"));
        Assert.Equal("sm2", await ScalarAsync<string>($"SELECT algorithm FROM student_flashcard WHERE id = '{card.Id}'"));
    }

    [Fact(DisplayName = "ADR-005: o aluno do grupo controle que erra também ganha o cartão, e não vê o tipo de erro")]
    public async Task Submit_InTheControlGroup_CreatesTheCardWithoutTheDiagnosis()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync(ExperimentalGroup.Control);

        // Act
        var result = await SubmitAsync(userId, content.Exercise.Id, content.Wrong.Id);

        // Assert
        Assert.Equal(AnswerFeedbackKind.Incorrect, result.Feedback.Kind);
        Assert.Null(result.Feedback.ErrorTypeName);
        Assert.NotNull(result.Flashcard);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM student_flashcard"));
    }

    [Fact(DisplayName = "A resposta traz o estado da lição já com ela, e o que ela conclui bate com o que o tópico mostra depois")]
    public async Task Submit_ReturnsTheLessonStateWithTheNewAnswer()
    {
        // Arrange: a lição de apoio tem dois exercícios
        var (content, userId, _) = await StudentInFractionsAsync();
        var support = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy, Difficulty.Easy);
        var correct = (Exercise exercise) => exercise.Alternatives.Single(alternative => alternative.IsCorrect).Id;
        var wrong = (Exercise exercise) => exercise.Alternatives.Single(alternative => !alternative.IsCorrect).Id;

        // Act
        var afterWrong = await SubmitAsync(userId, support[0].Id, wrong(support[0]));
        var afterFirst = await SubmitAsync(userId, support[0].Id, correct(support[0]));
        var afterSecond = await SubmitAsync(userId, support[1].Id, correct(support[1]));

        // Assert
        Assert.Equal((support[0].LessonId, LessonStatus.InProgress, 0, 2), (afterWrong.Lesson.Id, afterWrong.Lesson.Status, afterWrong.Lesson.CompletedExercises, afterWrong.Lesson.TotalExercises));
        Assert.Equal((LessonStatus.InProgress, 1, 2), (afterFirst.Lesson.Status, afterFirst.Lesson.CompletedExercises, afterFirst.Lesson.TotalExercises));
        Assert.Equal((LessonStatus.Completed, 2, 2), (afterSecond.Lesson.Status, afterSecond.Lesson.CompletedExercises, afterSecond.Lesson.TotalExercises));

        using var scope = NewScope();
        var topic = await scope.Get<ITopicReads>().FindAsync(userId, content.Fractions.Id, Cancel);
        var lesson = topic.Lessons.Single(candidate => candidate.Id == support[0].LessonId);
        Assert.Equal((LessonStatus.Completed, 2, 2), (lesson.Status, lesson.CompletedExercises, lesson.TotalExercises));
    }

    [Fact(DisplayName = "Acertar grava a tentativa e nenhum cartão")]
    public async Task Submit_WithTheRightAlternative_StoresNoCard()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync();

        // Act
        var result = await SubmitAsync(userId, content.Exercise.Id, content.Correct.Id);

        // Assert
        Assert.True(result.IsCorrect);
        Assert.Null(result.Flashcard);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM attempt"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM student_flashcard"));
    }

    [Fact(DisplayName = "Errar duas vezes o mesmo tipo de erro grava as duas tentativas e um cartão só")]
    public async Task Submit_TheSameErrorTwice_StoresTwoAttemptsAndOneCard()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync();

        // Act
        var first = await SubmitAsync(userId, content.Exercise.Id, content.Wrong.Id);
        var second = await SubmitAsync(userId, content.Exercise.Id, content.Wrong.Id);

        // Assert
        Assert.NotNull(first.Flashcard);
        Assert.Null(second.Flashcard);
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM attempt"));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM student_flashcard"));
        Assert.Equal(2, second.Topic.AttemptCount);
    }

    [Fact(DisplayName = "O tópico bloqueado é recusado com 403 e nada é gravado")]
    public async Task Submit_OnALockedTopic_WritesNothing()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainForbiddenException>(() => SubmitAsync(user.Id, content.Exercise.Id, content.Wrong.Id));

        // Assert
        Assert.Equal(StudentErrors.TopicLocked, exception.Code);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT (SELECT count(*) FROM attempt) + (SELECT count(*) FROM student_flashcard)"));
    }

    [Fact(DisplayName = "Uma alternativa de outro exercício é recusada e nada é gravado")]
    public async Task Submit_WithAnAlternativeOfAnotherExercise_WritesNothing()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync();
        var others = await this.SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy);
        var foreign = others[0].Alternatives.First();

        // Act
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => SubmitAsync(userId, content.Exercise.Id, foreign.Id));

        // Assert
        Assert.Equal(AnswerErrors.AlternativeNotInExercise, exception.Code);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM attempt"));
    }

    [Fact(DisplayName = "A quinta resposta certa domina a raiz e libera o dependente, e o progresso fica gravado")]
    public async Task Submit_TheFifthRightAnswer_MastersTheTopicAndStoresTheUnlock()
    {
        // Arrange: Operações básicas disponível, com cinco exercícios fáceis
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await this.UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Basics.Id, Clock));
        var exercises = await this.SeedExercisesAsync(content, content.Basics.Id, Enumerable.Repeat(Difficulty.Easy, 5).ToArray());

        // Act
        SubmitAttemptResult result = null!;
        foreach (var exercise in exercises)
        {
            result = await SubmitAsync(user.Id, exercise.Id, exercise.Alternatives.Single(alternative => alternative.IsCorrect).Id);
            Clock.Advance(TimeSpan.FromMinutes(1));
        }

        // Assert
        Assert.Equal((TopicStatus.Mastered, 5, 100.0m), (result.Topic.Status, result.Topic.AttemptCount, result.Topic.Score));
        Assert.Equal(content.Fractions.Id, Assert.Single(result.UnlockedTopics).Id);
        Assert.Equal(1L, await ScalarAsync<long>($"SELECT count(*) FROM student_topic WHERE topic_id = '{content.Basics.Id}' AND mastered_at IS NOT NULL"));
        Assert.Equal(1L, await ScalarAsync<long>($"SELECT count(*) FROM student_topic WHERE topic_id = '{content.Fractions.Id}' AND mastered_at IS NULL"));
    }

    [Fact(DisplayName = "FA-RSP-10: se outra requisição cria o mesmo cartão no meio do caminho, a tentativa é gravada e o cartão que vale é o da outra")]
    public async Task Submit_WhenTheCardIsCreatedByARacingRequest_KeepsTheAttempt()
    {
        // Arrange: o repositório do cartão responde "ainda não existe" e, logo depois, outra requisição grava o cartão do mesmo aluno
        var (content, userId, studentId) = await StudentInFractionsAsync();
        using var scope = NewScope();
        var racing = new RacingStudentFlashcardRepository(
            scope.StudentFlashcards,
            async () =>
            {
                using var other = NewScope();
                other.StudentFlashcards.Add(StudentFlashcard.Create(studentId, content.Generated.Id, null, new Sm2Scheduler(), Clock));
                await other.UnitOfWork.SaveChangesAsync(Cancel);
            });
        var handler = new SubmitAttemptHandler(
            scope.Users, scope.Students, scope.Exercises, scope.Get<ITopicRepository>(), scope.ErrorTypes, scope.Flashcards, racing,
            scope.Attempts, scope.Get<IAnswerReads>(), scope.Get<ILessonProgressReads>(), new Sm2Scheduler(), scope.UnitOfWork, Clock);

        // Act
        var result = await handler.Handle(userId, new SubmitAttemptCommand(content.Exercise.Id, content.Wrong.Id, null), Cancel);

        // Assert
        Assert.Null(result.Flashcard);
        Assert.Equal(1L, await ScalarAsync<long>($"SELECT count(*) FROM attempt WHERE id = '{result.AttemptId}'"));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM student_flashcard"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM student_flashcard WHERE origin_attempt_id IS NOT NULL"));
    }

    [Fact(DisplayName = "O nome do índice único que o caso de uso reconhece é o do banco")]
    public async Task Submit_TheUniqueIndexNameMatchesTheDatabase()
    {
        // Act
        var exists = await ScalarAsync<bool>(
            $"SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = '{SubmitAttemptHandler.StudentFlashcardUniqueIndex}')");

        // Assert
        Assert.True(exists);
    }

    /// <summary>Um repositório do cartão que deixa outra requisição criar o cartão entre a conferência e a gravação.</summary>
    private sealed class RacingStudentFlashcardRepository(IStudentFlashcardRepository inner, Func<Task> otherRequest)
        : IStudentFlashcardRepository
    {
        public Task<StudentFlashcard?> FindAsync(Guid id, Guid studentId, CancellationToken cancellationToken) =>
            inner.FindAsync(id, studentId, cancellationToken);

        public async Task<bool> ExistsAsync(Guid studentId, Guid flashcardId, CancellationToken cancellationToken)
        {
            var exists = await inner.ExistsAsync(studentId, flashcardId, cancellationToken);
            await otherRequest();
            return exists;
        }

        public void Add(StudentFlashcard card) => inner.Add(card);

        public void Discard(StudentFlashcard card) => inner.Discard(card);
    }
}
