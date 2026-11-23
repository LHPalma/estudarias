using Estudarias.Application.ErrorTypes;
using Estudarias.Domain.ErrorTypes;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class ErrorTypeRepository(AppDbContext db) : IErrorTypeRepository
{
    public Task<ErrorType?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.ErrorTypes.AsNoTracking().FirstOrDefaultAsync(errorType => errorType.Id == id, cancellationToken);
}
