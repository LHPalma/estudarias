using System.Collections.Concurrent;
using Estudarias.Application.Students;
using Estudarias.Application.Topics;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;

namespace Estudarias.Infrastructure.Persistence.InMemory;

// Implementações provisórias, até o EF Core + PostgreSQL entrarem. Os dados vivem só
// enquanto o processo roda; os testes funcionais os populam com Add.

public sealed class InMemoryTopicRepository : ITopicRepository
{
    private readonly ConcurrentDictionary<Guid, Topic> _items = new();

    public void Add(Topic topic) => _items[topic.Id] = topic;

    public Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(id));
}

public sealed class InMemoryStudentRepository : IStudentRepository
{
    private readonly ConcurrentDictionary<Guid, Student> _items = new();

    public void Add(Student student) => _items[student.Id] = student;

    public Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(id));
}
