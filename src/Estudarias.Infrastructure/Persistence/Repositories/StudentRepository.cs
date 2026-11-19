using Estudarias.Application.Students;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class StudentRepository(AppDbContext db) : IStudentRepository
{
    // O progresso é uma coleção owned: vem junto com o aluno, sem Include.
    public Task<Student?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Students.FirstOrDefaultAsync(student => student.Id == id, cancellationToken);

    public void Add(Student student) => db.Students.Add(student);
}
