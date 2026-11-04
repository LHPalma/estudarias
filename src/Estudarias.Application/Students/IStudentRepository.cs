using Estudarias.Domain.Students;

namespace Estudarias.Application.Students;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
