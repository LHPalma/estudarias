using Estudarias.Application.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class StudentFlashcardRepository(AppDbContext db) : IStudentFlashcardRepository
{
    public Task<StudentFlashcard?> FindAsync(Guid id, Guid studentId, CancellationToken cancellationToken) =>
        db.StudentFlashcards.FirstOrDefaultAsync(card => card.Id == id && card.StudentId == studentId, cancellationToken);

    public Task<bool> ExistsAsync(Guid studentId, Guid flashcardId, CancellationToken cancellationToken) =>
        db.StudentFlashcards.AnyAsync(card => card.StudentId == studentId && card.FlashcardId == flashcardId, cancellationToken);

    public void Add(StudentFlashcard card) => db.StudentFlashcards.Add(card);

    public void Discard(StudentFlashcard card) => db.Entry(card).State = EntityState.Detached;
}
