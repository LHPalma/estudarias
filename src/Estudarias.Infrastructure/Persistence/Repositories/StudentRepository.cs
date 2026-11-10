using Estudarias.Application.Students;
using Estudarias.Domain.Students;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class StudentRepository(AppDbContext db) : IStudentRepository
{
    public void Add(Student student) => db.Students.Add(student);
}
