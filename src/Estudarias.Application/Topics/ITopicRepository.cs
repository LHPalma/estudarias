using Estudarias.Domain.Topics;

namespace Estudarias.Application.Topics;

public interface ITopicRepository
{
    Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
