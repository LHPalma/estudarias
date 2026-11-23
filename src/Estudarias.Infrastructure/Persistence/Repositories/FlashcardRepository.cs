using Estudarias.Application.Flashcards;
using Estudarias.Domain.Flashcards;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence.Repositories;

internal sealed class FlashcardRepository(AppDbContext db) : IFlashcardRepository
{
    public Task<Flashcard?> FindGeneratedByAsync(Guid errorTypeId, CancellationToken cancellationToken) =>
        db.Flashcards.AsNoTracking().FirstOrDefaultAsync(flashcard => flashcard.ErrorTypeId == errorTypeId, cancellationToken);
}
