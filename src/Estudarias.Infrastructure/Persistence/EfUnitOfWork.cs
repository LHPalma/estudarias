using Estudarias.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estudarias.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } postgresError)
        {
            throw new UniqueConstraintViolationException(postgresError.ConstraintName, exception);
        }
    }
}
