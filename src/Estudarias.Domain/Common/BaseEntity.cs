namespace Estudarias.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; private init; }

    protected BaseEntity() => Id = Guid.CreateVersion7();
}
