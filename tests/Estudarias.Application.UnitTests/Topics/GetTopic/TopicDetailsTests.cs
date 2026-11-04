using Estudarias.Application.Topics.GetTopic;
using Estudarias.Domain.Students;

namespace Estudarias.Application.UnitTests.Topics.GetTopic;

public class TopicDetailsTests
{
    [Theory(DisplayName = "Um tópico está desbloqueado quando está disponível ou dominado")]
    [InlineData(TopicStatus.Locked, false)]
    [InlineData(TopicStatus.Available, true)]
    [InlineData(TopicStatus.Mastered, true)]
    public void IsUnlocked_FollowsStatus(TopicStatus status, bool expected)
    {
        // Arrange
        var details = new TopicDetails(Guid.CreateVersion7(), "Frações", "fracoes", status);

        // Act & Assert
        Assert.Equal(expected, details.IsUnlocked);
    }
}
