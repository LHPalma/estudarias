using Estudarias.Application.Common;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Reviews.Queries;

public sealed class ReviewReads(IApplicationDbContext db, TimeProvider clock) : IReviewReads
{
    public async Task<DueReviewsView> DueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var studentId = await StudentReads.StudentIdOfAsync(db, userId, cancellationToken)
            ?? throw IdentityErrors.InvalidSession();

        var now = clock.GetUtcNow();

        // Uma consulta, pelo índice (student_id, next_review_at), e só as colunas do contrato: o estado de agendamento (jsonb) é do
        // adapter do algoritmo e nunca sai numa leitura. Do mais atrasado ao mais recente, e o id desempata para duas revisões no
        // mesmo instante saírem sempre na mesma ordem (RN-REV-10).
        var items = await (
            from card in db.StudentFlashcards
            join flashcard in db.Flashcards on card.FlashcardId equals flashcard.Id
            join topic in db.Topics on flashcard.TopicId equals topic.Id
            where card.StudentId == studentId && card.NextReviewAt <= now
            orderby card.NextReviewAt, card.Id
            select new DueCardView(card.Id, flashcard.Question, flashcard.Answer, topic.Id, topic.Name, card.NextReviewAt))
            .ToListAsync(cancellationToken);

        return new DueReviewsView(items.Count, items);
    }
}
