using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Trail;

/// <summary>
/// O estado da lição para o aluno (#91) nas leituras do tópico e da trilha, contra um PostgreSQL real: derivado das tentativas, sem estado
/// novo, com os exercícios acertados contados uma vez cada.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LessonProgressReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<TopicView> TopicAsync(Guid userId, Guid topicId)
    {
        using var scope = NewScope();
        return await scope.Get<ITopicReads>().FindAsync(userId, topicId, Cancel);
    }

    private async Task<TrailTopicView> TrailTopicAsync(Guid userId, Guid topicId)
    {
        using var scope = NewScope();
        var trail = (await scope.Get<ITrailReads>().FindAsync(userId, Cancel))!;
        return trail.Topics.Single(topic => topic.Id == topicId);
    }

    private static TopicLessonView LessonOf(TopicView topic, Guid lessonId) => topic.Lessons.Single(lesson => lesson.Id == lessonId);

    private async Task<(SampleContent Content, Guid UserId, Guid StudentId)> StudentInFractionsAsync(
        string email = "ana@example.com", ExperimentalGroup group = ExperimentalGroup.Diagnostic)
    {
        var content = await SeedContentAsync();
        var user = await SeedUserAsync(email, group);
        await this.UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        return (content, user.Id, user.StudentId!.Value);
    }

    /// <summary>A lição de apoio que o <see cref="LearningSeeds"/> cria: devolve o id da lição e os exercícios.</summary>
    private async Task<(Guid LessonId, List<Exercise> Exercises)> SupportLessonAsync(SampleContent content, int exercises)
    {
        var created = await this.SeedExercisesAsync(content, content.Fractions.Id, Enumerable.Repeat(Difficulty.Easy, exercises).ToArray());
        return (created[0].LessonId, created);
    }

    [Fact(DisplayName = "Sem nenhuma tentativa, toda lição do tópico não foi iniciada")]
    public async Task Topic_WithoutAttempts_EveryLessonIsNotStarted()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync();

        // Act
        var lesson = LessonOf(await TopicAsync(userId, content.Fractions.Id), content.Lesson.Id);

        // Assert
        Assert.Equal((LessonStatus.NotStarted, 0, 1), (lesson.Status, lesson.CompletedExercises, lesson.TotalExercises));
    }

    [Fact(DisplayName = "Errar o exercício deixa a lição em andamento, 0 de 1, e acertá-lo a conclui, 1 de 1")]
    public async Task Topic_WrongThenRight_GoesFromInProgressToCompleted()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();

        // Act & Assert
        await this.AnswerAsync(studentId, content.Exercise, correct: false);
        var afterWrong = LessonOf(await TopicAsync(userId, content.Fractions.Id), content.Lesson.Id);
        Assert.Equal((LessonStatus.InProgress, 0, 1), (afterWrong.Status, afterWrong.CompletedExercises, afterWrong.TotalExercises));

        await this.AnswerAsync(studentId, content.Exercise, correct: true);
        var afterRight = LessonOf(await TopicAsync(userId, content.Fractions.Id), content.Lesson.Id);
        Assert.Equal((LessonStatus.Completed, 1, 1), (afterRight.Status, afterRight.CompletedExercises, afterRight.TotalExercises));
    }

    [Fact(DisplayName = "Com 2 de 3 exercícios acertados a lição está em andamento, e o terceiro a conclui")]
    public async Task Topic_CountsTheExercisesAnsweredRight()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();
        var (lessonId, exercises) = await SupportLessonAsync(content, 3);
        await this.AnswerAsync(studentId, exercises[0], correct: true);
        await this.AnswerAsync(studentId, exercises[1], correct: true);

        // Act
        var partial = LessonOf(await TopicAsync(userId, content.Fractions.Id), lessonId);
        await this.AnswerAsync(studentId, exercises[2], correct: true);
        var complete = LessonOf(await TopicAsync(userId, content.Fractions.Id), lessonId);

        // Assert
        Assert.Equal((LessonStatus.InProgress, 2, 3), (partial.Status, partial.CompletedExercises, partial.TotalExercises));
        Assert.Equal((LessonStatus.Completed, 3, 3), (complete.Status, complete.CompletedExercises, complete.TotalExercises));
    }

    [Fact(DisplayName = "Acertar o mesmo exercício várias vezes conta uma vez só")]
    public async Task Topic_CountsEachExerciseOnce()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();
        var (lessonId, exercises) = await SupportLessonAsync(content, 2);
        await this.AnswerAsync(studentId, exercises[0], correct: true);
        await this.AnswerAsync(studentId, exercises[0], correct: true);
        await this.AnswerAsync(studentId, exercises[0], correct: true);

        // Act
        var lesson = LessonOf(await TopicAsync(userId, content.Fractions.Id), lessonId);

        // Assert
        Assert.Equal((LessonStatus.InProgress, 1, 2), (lesson.Status, lesson.CompletedExercises, lesson.TotalExercises));
    }

    [Fact(DisplayName = "Errar depois um exercício já acertado não desfaz a conclusão")]
    public async Task Topic_AWrongAnswerAfterARightOne_KeepsTheLessonCompleted()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();
        await this.AnswerAsync(studentId, content.Exercise, correct: true);
        await this.AnswerAsync(studentId, content.Exercise, correct: false);

        // Act
        var lesson = LessonOf(await TopicAsync(userId, content.Fractions.Id), content.Lesson.Id);

        // Assert
        Assert.Equal(LessonStatus.Completed, lesson.Status);
    }

    [Fact(DisplayName = "Um exercício acrescentado a uma lição concluída a faz voltar a em andamento, 1 de 2")]
    public async Task Topic_ANewExerciseOnACompletedLesson_BringsItBackToInProgress()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();
        await this.AnswerAsync(studentId, content.Exercise, correct: true);
        var added = Exercise.Create(
            content.Lesson.Id, 2, "exercicio_novo", "Pergunta nova?", Difficulty.Easy,
            [Alternative.Correct("certa"), Alternative.Wrong("errada", content.ErrorType.Id)]);
        await WriteAsync(db => db.Exercises.Add(added));

        // Act
        var lesson = LessonOf(await TopicAsync(userId, content.Fractions.Id), content.Lesson.Id);

        // Assert
        Assert.Equal((LessonStatus.InProgress, 1, 2), (lesson.Status, lesson.CompletedExercises, lesson.TotalExercises));
    }

    [Fact(DisplayName = "Uma lição sem exercícios não foi iniciada, 0 de 0, e nunca conta como concluída")]
    public async Task Topic_ALessonWithoutExercises_IsNotStartedWithZeroOfZero()
    {
        // Arrange
        var (content, userId, _) = await StudentInFractionsAsync();
        var empty = Lesson.Create(content.Fractions.Id, 7, "Lição sem exercícios", "Só texto.");
        await WriteAsync(db => db.Lessons.Add(empty));

        // Act
        var topic = await TopicAsync(userId, content.Fractions.Id);
        var trailTopic = await TrailTopicAsync(userId, content.Fractions.Id);

        // Assert
        var lesson = LessonOf(topic, empty.Id);
        Assert.Equal((LessonStatus.NotStarted, 0, 0), (lesson.Status, lesson.CompletedExercises, lesson.TotalExercises));
        Assert.Equal((0, 2), (trailTopic.CompletedLessons, trailTopic.TotalLessons));
    }

    [Fact(DisplayName = "As respostas de outro aluno e as de outra lição não contam para a lição")]
    public async Task Topic_IgnoresTheAnswersOfAnotherStudentAndOfAnotherLesson()
    {
        // Arrange
        var (content, anaId, anaStudent) = await StudentInFractionsAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        await this.UpdateStudentAsync(bia.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        var (otherLessonId, others) = await SupportLessonAsync(content, 1);
        await this.AnswerAsync(bia.StudentId!.Value, content.Exercise, correct: true);
        await this.AnswerAsync(anaStudent, others[0], correct: true);

        // Act
        var topic = await TopicAsync(anaId, content.Fractions.Id);

        // Assert
        Assert.Equal(LessonStatus.NotStarted, LessonOf(topic, content.Lesson.Id).Status);
        Assert.Equal(LessonStatus.Completed, LessonOf(topic, otherLessonId).Status);
    }

    [Fact(DisplayName = "RN-TRL-15: os dois grupos experimentais veem o mesmo estado da lição")]
    public async Task Topic_TheTwoGroupsSeeTheSameLessonState()
    {
        // Arrange
        var (content, diagnosticId, diagnosticStudent) = await StudentInFractionsAsync("ana@example.com", ExperimentalGroup.Diagnostic);
        var control = await SeedUserAsync("bia@example.com", ExperimentalGroup.Control);
        await this.UpdateStudentAsync(control.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        await this.AnswerAsync(diagnosticStudent, content.Exercise, correct: false);
        await this.AnswerAsync(control.StudentId!.Value, content.Exercise, correct: false);

        // Act
        var diagnostic = LessonOf(await TopicAsync(diagnosticId, content.Fractions.Id), content.Lesson.Id);
        var controlLesson = LessonOf(await TopicAsync(control.Id, content.Fractions.Id), content.Lesson.Id);

        // Assert
        Assert.Equal(diagnostic with { Id = default }, controlLesson with { Id = default });
    }

    [Fact(DisplayName = "RN-TRL-14: concluir a lição não domina o tópico: o status segue disponível")]
    public async Task Topic_CompletingTheLessonDoesNotMasterTheTopic()
    {
        // Arrange
        var (content, userId, studentId) = await StudentInFractionsAsync();
        await this.AnswerAsync(studentId, content.Exercise, correct: true);

        // Act
        var topic = await TopicAsync(userId, content.Fractions.Id);

        // Assert
        Assert.Equal(LessonStatus.Completed, LessonOf(topic, content.Lesson.Id).Status);
        Assert.Equal(TopicStatus.Available, topic.Status);
    }

    [Fact(DisplayName = "A trilha traz as lições concluídas e as totais de cada tópico")]
    public async Task Trail_ReturnsTheCompletedAndTotalLessonsOfEachTopic()
    {
        // Arrange: Frações tem a lição de exemplo e uma de apoio com dois exercícios; Operações básicas não tem lição
        var (content, userId, studentId) = await StudentInFractionsAsync();
        var (_, support) = await SupportLessonAsync(content, 2);
        await this.AnswerAsync(studentId, content.Exercise, correct: true);
        await this.AnswerAsync(studentId, support[0], correct: true);

        // Act
        var fractions = await TrailTopicAsync(userId, content.Fractions.Id);
        var basics = await TrailTopicAsync(userId, content.Basics.Id);

        // Assert
        Assert.Equal((1, 2), (fractions.CompletedLessons, fractions.TotalLessons));
        Assert.Equal((0, 0), (basics.CompletedLessons, basics.TotalLessons));
    }

    [Fact(DisplayName = "Um aluno novo vê 0 lições concluídas, de todas as lições do tópico, mesmo no tópico bloqueado")]
    public async Task Trail_ForANewStudent_HasNoCompletedLessons()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var fractions = await TrailTopicAsync(user.Id, content.Fractions.Id);

        // Assert
        Assert.Equal((0, 1), (fractions.CompletedLessons, fractions.TotalLessons));
    }
}
