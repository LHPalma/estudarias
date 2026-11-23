using Estudarias.Application.Trail;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Estudarias.Infrastructure.IntegrationTests.Trail;

/// <summary>A leitura da lição (#87) contra um PostgreSQL real: conteúdo, ordem, o acesso e, acima de tudo, o gabarito que nunca sai.</summary>
[Collection(PostgresCollection.Name)]
public class LessonReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private async Task<LessonView> FindAsync(Guid userId, Guid lessonId)
    {
        using var scope = NewScope();
        return await scope.Get<ILessonReads>().FindAsync(userId, lessonId, Cancel);
    }

    private async Task<User> UserWithFractionsUnlockedAsync(SampleContent content)
    {
        var user = await SeedUserAsync();
        await this.UpdateStudentAsync(user.StudentId!.Value, student => student.Unlock(content.Fractions.Id, Clock));
        return user;
    }

    [Fact(DisplayName = "A lição traz o conteúdo, o tópico, a ordem e os exercícios com as alternativas")]
    public async Task Find_ReturnsTheLessonWithItsExercises()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await UserWithFractionsUnlockedAsync(content);

        // Act
        var lesson = await FindAsync(user.Id, content.Lesson.Id);

        // Assert
        Assert.Equal(
            (content.Lesson.Id, content.Fractions.Id, 1, "Soma de frações", "Para somar {1/2} + {1/3}, encontre um denominador comum."),
            (lesson.Id, lesson.TopicId, lesson.Order, lesson.Title, lesson.Content));
        var exercise = Assert.Single(lesson.Exercises);
        Assert.Equal((content.Exercise.Id, "Quanto é 1/2 + 1/3?", Difficulty.Medium), (exercise.Id, exercise.Statement, exercise.Difficulty));
        Assert.Equal(["5/6", "2/5"], exercise.Alternatives.Select(alternative => alternative.Text));
        Assert.Equal(content.Exercise.Alternatives.Select(alternative => alternative.Id), exercise.Alternatives.Select(alternative => alternative.Id));
    }

    [Fact(DisplayName = "Os exercícios e as alternativas vêm na ordem gravada do conteúdo, e não na do id")]
    public async Task Find_ReturnsTheExercisesAndAlternativesInTheStoredOrder()
    {
        // Arrange: a correta no meio, e a ordem dos exercícios na lição diferente da de criação
        var content = await SeedContentAsync();
        var user = await UserWithFractionsUnlockedAsync(content);
        var later = Exercise.Create(
            content.Lesson.Id, 3, "terceiro", "Terceiro?", Difficulty.Easy,
            [Alternative.Wrong("z", content.ErrorType.Id), Alternative.Correct("m"), Alternative.Wrong("a", content.ErrorType.Id)]);
        var earlier = Exercise.Create(
            content.Lesson.Id, 2, "segundo", "Segundo?", Difficulty.Hard,
            [Alternative.Wrong("w", content.ErrorType.Id), Alternative.Correct("k")]);
        await WriteAsync(db => db.Exercises.AddRange(later, earlier));

        // Act
        var lesson = await FindAsync(user.Id, content.Lesson.Id);

        // Assert
        Assert.Equal(["Quanto é 1/2 + 1/3?", "Segundo?", "Terceiro?"], lesson.Exercises.Select(exercise => exercise.Statement));
        Assert.Equal(["z", "m", "a"], lesson.Exercises[2].Alternatives.Select(alternative => alternative.Text));
        Assert.Equal(["w", "k"], lesson.Exercises[1].Alternatives.Select(alternative => alternative.Text));
    }

    [Fact(DisplayName = "O resultado não tem nenhum campo que revele a alternativa correta")]
    public async Task Find_HasNoFieldThatRevealsTheCorrectAlternative()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await UserWithFractionsUnlockedAsync(content);

        // Act
        var lesson = await FindAsync(user.Id, content.Lesson.Id);

        // Assert: a alternativa só tem id e texto, e o exercício, nada de correção
        Assert.Equal(["Id", "Text"], typeof(LessonAlternativeView).GetProperties().Select(property => property.Name).Order());
        Assert.Equal(
            ["Alternatives", "Difficulty", "Id", "Statement"],
            typeof(LessonExerciseView).GetProperties().Select(property => property.Name).Order());
        Assert.DoesNotContain(
            typeof(LessonView).GetProperties().Select(property => property.Name),
            name => name.Contains("Correct", StringComparison.OrdinalIgnoreCase) || name.Contains("ErrorType", StringComparison.OrdinalIgnoreCase));
        Assert.Single(lesson.Exercises);
    }

    [Fact(DisplayName = "A consulta nunca lê is_correct nem error_type_id: o gabarito nem sai do banco")]
    public async Task Find_NeverSelectsTheAnswerKey()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await UserWithFractionsUnlockedAsync(content);
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options;
        await using var context = new AppDbContext(options);

        // Act
        await new LessonReads(context).FindAsync(user.Id, content.Lesson.Id, Cancel);

        // Assert
        var sql = string.Join('\n', commands);
        Assert.True(sql.Contains("alternative", StringComparison.Ordinal), sql);
        Assert.DoesNotContain("is_correct", sql);
        Assert.DoesNotContain("error_type_id", sql);
    }

    [Fact(DisplayName = "A lição de um tópico bloqueado é student.topic.locked, e o 403 não revela o conteúdo")]
    public async Task Find_ForALockedTopic_ThrowsForbidden()
    {
        // Arrange: Frações não foi liberado
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainForbiddenException>(() => FindAsync(user.Id, content.Lesson.Id));

        // Assert
        Assert.Equal(StudentErrors.TopicLocked, exception.Code);
        Assert.DoesNotContain("denominador", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Dominado o tópico, a lição continua aberta")]
    public async Task Find_ForAMasteredTopic_Opens()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await this.UpdateStudentAsync(user.StudentId!.Value, student =>
        {
            student.Unlock(content.Fractions.Id, Clock);
            student.MarkMastered(content.Fractions.Id, Clock);
        });

        // Act
        var lesson = await FindAsync(user.Id, content.Lesson.Id);

        // Assert
        Assert.Equal(content.Lesson.Id, lesson.Id);
    }

    [Fact(DisplayName = "Uma lição que não existe é trail.lesson.notFound, antes de qualquer conferência do tópico")]
    public async Task Find_ForAnUnknownLesson_ThrowsNotFound()
    {
        // Arrange
        await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() => FindAsync(user.Id, Guid.CreateVersion7()));

        // Assert
        Assert.Equal(TrailErrors.LessonNotFound, exception.Code);
    }

    [Fact(DisplayName = "Uma conta que não existe é sessão inválida")]
    public async Task Find_ForAnUnknownAccount_ThrowsInvalidSession()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => FindAsync(Guid.CreateVersion7(), content.Lesson.Id));

        // Assert
        Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
    }
}
