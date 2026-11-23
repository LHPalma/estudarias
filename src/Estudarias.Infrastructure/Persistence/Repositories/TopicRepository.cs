using Estudarias.Application.Topics;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class TopicRepository(AppDbContext db) : ITopicRepository
{
    // Os pré-requisitos são uma coleção owned: vêm junto com o tópico, sem Include. Sem rastreamento, porque o chamador só lê o grafo.
    public async Task<IReadOnlyList<Topic>> ListAsync(CancellationToken cancellationToken) =>
        await db.Topics.AsNoTracking().ToListAsync(cancellationToken);
}
