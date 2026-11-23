using Estudarias.Domain.Exercises;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

/// <summary>
/// Os repositórios de leitura do conteúdo que o caso de uso de resposta usa (#88): o exercício com o tópico, o tipo de erro e o flashcard
/// gerado por ele. O conteúdo só é escrito pela carga, então os testes só leem.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ContentRepositoriesTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    [Fact(DisplayName = "O exercício volta com as alternativas na ordem do conteúdo, a correta e os tipos de erro, e com o tópico da lição")]
    public async Task Exercise_FindAsync_ReturnsTheExerciseWithItsTopic()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        using var scope = NewScope();
        var found = await scope.Exercises.FindAsync(content.Exercise.Id, Cancel);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(content.Fractions.Id, found.TopicId);
        Assert.Equal(["5/6", "2/5"], found.Exercise.Alternatives.Select(alternative => alternative.Text));
        Assert.Equal(content.Correct.Id, found.Exercise.Alternatives.Single(alternative => alternative.IsCorrect).Id);
        Assert.Equal(content.ErrorType.Id, found.Exercise.Alternatives.Single(alternative => !alternative.IsCorrect).ErrorTypeId);
    }

    [Fact(DisplayName = "Um exercício que não existe volta nulo")]
    public async Task Exercise_FindAsync_ForAnUnknownExercise_ReturnsNull()
    {
        // Arrange
        await SeedContentAsync();

        // Act
        using var scope = NewScope();
        var found = await scope.Exercises.FindAsync(Guid.CreateVersion7(), Cancel);

        // Assert
        Assert.Null(found);
    }

    [Fact(DisplayName = "O exercício lido para corrigir não é rastreado: nada dele é gravado por engano")]
    public async Task Exercise_FindAsync_IsNotTracked()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        using var scope = NewScope();
        await scope.Exercises.FindAsync(content.Exercise.Id, Cancel);

        // Assert
        Assert.Empty(scope.Get<Estudarias.Infrastructure.Persistence.AppDbContext>().ChangeTracker.Entries());
    }

    [Fact(DisplayName = "O tipo de erro volta com o nome e o feedback")]
    public async Task ErrorType_FindAsync_ReturnsTheErrorType()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        using var scope = NewScope();
        var found = await scope.ErrorTypes.FindAsync(content.ErrorType.Id, Cancel);

        // Assert
        Assert.NotNull(found);
        Assert.Equal((content.ErrorType.Name, content.ErrorType.Feedback), (found.Name, found.Feedback));
        Assert.Null(await scope.ErrorTypes.FindAsync(Guid.CreateVersion7(), Cancel));
    }

    [Fact(DisplayName = "O flashcard do tipo de erro é o gerado por ele, e não o manual do mesmo tópico")]
    public async Task Flashcard_FindGeneratedByAsync_ReturnsTheGeneratedOne()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        using var scope = NewScope();
        var found = await scope.Flashcards.FindGeneratedByAsync(content.ErrorType.Id, Cancel);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(content.Generated.Id, found.Id);
        Assert.NotEqual(content.Manual.Id, found.Id);
    }

    [Fact(DisplayName = "Um tipo de erro sem flashcard gerado volta nulo")]
    public async Task Flashcard_FindGeneratedByAsync_WithoutOne_ReturnsNull()
    {
        // Arrange
        await SeedContentAsync();

        // Act
        using var scope = NewScope();
        var found = await scope.Flashcards.FindGeneratedByAsync(Guid.CreateVersion7(), Cancel);

        // Assert
        Assert.Null(found);
    }
}
