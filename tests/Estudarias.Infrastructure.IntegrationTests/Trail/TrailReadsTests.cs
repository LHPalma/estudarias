using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Attempts;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Trail;

/// <summary>A leitura da trilha (#86) contra um PostgreSQL real: status, nota, revisões vencidas e a ordem dos tópicos.</summary>
[Collection(PostgresCollection.Name)]
public class TrailReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private static readonly Sm2Scheduler Scheduler = new();

    private async Task<TrailView?> FindAsync(Guid userId)
    {
        using var scope = NewScope();
        return await scope.Get<ITrailReads>().FindAsync(userId, Cancel);
    }

    private async Task<TrailView> TrailOfAsync(User user) => (await FindAsync(user.Id))!;

    private static TrailTopicView Topic(TrailView trail, Guid id) => trail.Topics.Single(topic => topic.Id == id);

    private async Task UpdateStudentAsync(Guid studentId, Action<Student> change)
    {
        using var scope = NewScope();
        var student = (await scope.Students.FindAsync(studentId, Cancel))!;
        change(student);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
    }

    /// <summary>Uma lição com um exercício de cada dificuldade pedida, no tópico: devolve os exercícios, com a alternativa certa e a errada.</summary>
    private async Task<List<Exercise>> SeedExercisesAsync(SampleContent content, Guid topicId, params Difficulty[] difficulties)
    {
        var lesson = Lesson.Create(topicId, 9, "Lição de apoio", "Conteúdo.");
        var exercises = difficulties
            .Select((difficulty, index) => Exercise.Create(
                lesson.Id, index + 1,
                $"apoio_{lesson.Id:N}_{index}",
                $"Pergunta {index}",
                difficulty,
                [Alternative.Correct("certa"), Alternative.Wrong("errada", content.ErrorType.Id)]))
            .ToList();

        await WriteAsync(db =>
        {
            db.Lessons.Add(lesson);
            db.Exercises.AddRange(exercises);
        });

        return exercises;
    }

    private async Task AnswerAsync(Guid studentId, Exercise exercise, bool correct)
    {
        var alternative = exercise.Alternatives.Single(candidate => candidate.IsCorrect == correct);
        using var scope = NewScope();
        scope.Attempts.Add(Attempt.Create(studentId, exercise.Id, alternative.Id, null, Clock));
        await scope.UnitOfWork.SaveChangesAsync(Cancel);

        // Cada resposta num instante próprio: a ordem da mais recente para a mais antiga não depende de desempate.
        Clock.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "A trilha traz os tópicos em ordem de pré-requisito, com o status, os pré-requisitos e os totais")]
    public async Task Find_ReturnsTheTopicsInOrderWithStatusAndTotals()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Basics.Id, Clock));

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.Equal(70m, trail.MasteryThreshold);
        Assert.Equal(2, trail.TotalTopics);
        Assert.Equal(0, trail.MasteredTopics);
        Assert.Equal(0, trail.DueReviews);
        Assert.Equal([content.Basics.Id, content.Fractions.Id], trail.Topics.Select(topic => topic.Id));
        Assert.Equal(["operacoes-basicas", "fracoes"], trail.Topics.Select(topic => topic.Slug));
        Assert.Equal([TopicStatus.Available, TopicStatus.Locked], trail.Topics.Select(topic => topic.Status));
        Assert.Empty(trail.Topics[0].Prerequisites);
        Assert.Equal([content.Basics.Id], trail.Topics[1].Prerequisites);
    }

    [Fact(DisplayName = "Um tópico sem nenhuma resposta tem nota nula")]
    public async Task Find_WithoutAnswers_HasNoScore()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.All(trail.Topics, topic => Assert.Null(topic.Score));
        Assert.Equal(content.Fractions.Id, trail.Topics[1].Id);
    }

    [Fact(DisplayName = "Sem progresso gravado, todo tópico está bloqueado")]
    public async Task Find_WithoutProgress_LocksEveryTopic()
    {
        // Arrange
        await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.All(trail.Topics, topic => Assert.Equal(TopicStatus.Locked, topic.Status));
    }

    [Fact(DisplayName = "O tópico dominado conta nos dominados, e o dependente liberado fica disponível")]
    public async Task Find_CountsTheMasteredTopics()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await UpdateStudentAsync(user.StudentId!.Value, student =>
        {
            student.Unlock(content.Basics.Id, Clock);
            student.MarkMastered(content.Basics.Id, Clock);
            student.Unlock(content.Fractions.Id, Clock);
        });

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.Equal(1, trail.MasteredTopics);
        Assert.Equal([TopicStatus.Mastered, TopicStatus.Available], trail.Topics.Select(topic => topic.Status));
    }

    [Fact(DisplayName = "A nota é a do TopicScore sobre as respostas do tópico, arredondada para baixo com uma casa")]
    public async Task Find_ComputesTheScoreFromTheAnswers()
    {
        // Arrange: a mais antiga errada, a mais recente certa, as duas médias: 100 × 2 / (2 + 1,8) = 52,6315...
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var exercises = await SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Medium, Difficulty.Medium);
        await AnswerAsync(user.StudentId!.Value, exercises[0], correct: false);
        await AnswerAsync(user.StudentId!.Value, exercises[1], correct: true);

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.Equal(52.6m, Topic(trail, content.Fractions.Id).Score);
        Assert.Null(Topic(trail, content.Basics.Id).Score);
    }

    [Fact(DisplayName = "Só as 10 respostas mais recentes entram na nota")]
    public async Task Find_ScoreUsesOnlyTheLatestTenAnswers()
    {
        // Arrange: uma resposta errada, a mais antiga, e depois dez certas
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var exercises = await SeedExercisesAsync(content, content.Fractions.Id, Enumerable.Repeat(Difficulty.Easy, 11).ToArray());
        await AnswerAsync(user.StudentId!.Value, exercises[0], correct: false);
        foreach (var exercise in exercises.Skip(1))
        {
            await AnswerAsync(user.StudentId!.Value, exercise, correct: true);
        }

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.Equal(100.0m, Topic(trail, content.Fractions.Id).Score);
    }

    [Fact(DisplayName = "A nota de cada tópico vem só das respostas dele, e não das de outro tópico")]
    public async Task Find_ScoreIsPerTopic()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var basics = await SeedExercisesAsync(content, content.Basics.Id, Difficulty.Easy);
        var fractions = await SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy);
        await AnswerAsync(user.StudentId!.Value, basics[0], correct: true);
        await AnswerAsync(user.StudentId!.Value, fractions[0], correct: false);

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.Equal(100.0m, Topic(trail, content.Basics.Id).Score);
        Assert.Equal(0.0m, Topic(trail, content.Fractions.Id).Score);
    }

    [Fact(DisplayName = "As respostas de outro aluno não entram na nota")]
    public async Task Find_IgnoresTheAnswersOfAnotherStudent()
    {
        // Arrange
        var content = await SeedContentAsync();
        var ana = await SeedUserAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        var exercises = await SeedExercisesAsync(content, content.Fractions.Id, Difficulty.Easy);
        await AnswerAsync(bia.StudentId!.Value, exercises[0], correct: true);

        // Act
        var anaTrail = await TrailOfAsync(ana);
        var biaTrail = await TrailOfAsync(bia);

        // Assert
        Assert.Null(Topic(anaTrail, content.Fractions.Id).Score);
        Assert.Equal(100.0m, Topic(biaTrail, content.Fractions.Id).Score);
    }

    [Fact(DisplayName = "As revisões vencidas contam só os cartões do aluno cuja próxima revisão já chegou")]
    public async Task Find_CountsOnlyTheDueReviewsOfTheStudent()
    {
        // Arrange
        var content = await SeedContentAsync();
        var ana = await SeedUserAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");

        async Task AddCardAsync(Guid studentId, Guid flashcardId)
        {
            using var scope = NewScope();
            scope.StudentFlashcards.Add(StudentFlashcard.Create(studentId, flashcardId, null, Scheduler, Clock));
            await scope.UnitOfWork.SaveChangesAsync(Cancel);
        }

        await AddCardAsync(ana.StudentId!.Value, content.Generated.Id);
        await AddCardAsync(bia.StudentId!.Value, content.Generated.Id);
        Clock.Advance(TimeSpan.FromDays(30));
        await AddCardAsync(ana.StudentId!.Value, content.Manual.Id);

        // Act
        var anaTrail = await TrailOfAsync(ana);
        var biaTrail = await TrailOfAsync(bia);

        // Assert: o cartão novo da Ana ainda não venceu; o da Bia venceu, mas é dela
        Assert.Equal(1, anaTrail.DueReviews);
        Assert.Equal(1, biaTrail.DueReviews);
    }

    [Fact(DisplayName = "A trilha de um aluno sem conteúdo carregado é vazia")]
    public async Task Find_WithoutContent_IsEmpty()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        var trail = await TrailOfAsync(user);

        // Assert
        Assert.Empty(trail.Topics);
        Assert.Equal(0, trail.TotalTopics);
        Assert.Equal(0, trail.MasteredTopics);
    }

    [Fact(DisplayName = "Uma conta que não existe ou está desabilitada não tem trilha")]
    public async Task Find_ForAnUnknownOrDisabledAccount_ReturnsNull()
    {
        // Arrange
        var user = await SeedUserAsync();
        await ExecuteAsync($"UPDATE app_user SET is_active = false WHERE id = '{user.Id}'");

        // Act & Assert
        Assert.Null(await FindAsync(Guid.CreateVersion7()));
        Assert.Null(await FindAsync(user.Id));
    }

    [Fact(DisplayName = "Uma conta sem aluno ligado não tem trilha")]
    public async Task Find_ForAnAccountWithoutAStudent_ReturnsNull()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO app_user (id, email, role, student_id, is_active, password_pepper_version, password_hash) " +
            $"VALUES ('{userId}', 'sem-aluno@example.com', 'student', NULL, true, 1, '$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA')");

        // Act & Assert
        Assert.Null(await FindAsync(userId));
    }
}
