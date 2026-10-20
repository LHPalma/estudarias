using Estudarias.Domain.Common;

namespace Estudarias.Domain.UnitTests.Common;

public class BaseEntityTests
{
    private sealed class FakeEntity : BaseEntity;

    [Fact(DisplayName = "Toda entidade nasce com um Guid v7")]
    public void Constructor_GeneratesVersion7Id()
    {
        // Act
        var entity = new FakeEntity();

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(7, entity.Id.Version);
    }

    [Fact(DisplayName = "Duas entidades nunca recebem o mesmo id")]
    public void Constructor_GeneratesDistinctIds()
    {
        // Act
        var first = new FakeEntity();
        var second = new FakeEntity();

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }
}
