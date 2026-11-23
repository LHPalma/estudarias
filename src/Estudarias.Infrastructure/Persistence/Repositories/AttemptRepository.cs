using Estudarias.Application.Attempts;
using Estudarias.Domain.Attempts;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class AttemptRepository(AppDbContext db) : IAttemptRepository
{
    public void Add(Attempt attempt) => db.Attempts.Add(attempt);
}
