using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class TopicRepositoryTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    [Fact(DisplayName = "A lista traz todos os tópicos, cada um com os ids dos pré-requisitos")]
    public async Task ListAsync_ReturnsTheTopicsWithTheirPrerequisites()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        using var scope = NewScope();
        var topics = await scope.Topics.ListAsync(Cancel);

        // Assert
        Assert.Equal(2, topics.Count);
        Assert.Empty(topics.Single(topic => topic.Id == content.Basics.Id).Prerequisites);
        Assert.Equal([content.Basics.Id], topics.Single(topic => topic.Id == content.Fractions.Id).Prerequisites);
    }

    [Fact(DisplayName = "Sem conteúdo carregado, a lista é vazia")]
    public async Task ListAsync_WithoutContent_IsEmpty()
    {
        // Act
        using var scope = NewScope();
        var topics = await scope.Topics.ListAsync(Cancel);

        // Assert
        Assert.Empty(topics);
    }
}
