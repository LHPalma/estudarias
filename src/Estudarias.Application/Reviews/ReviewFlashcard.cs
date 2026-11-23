using Estudarias.Application.Common;
using Estudarias.Application.Identity;
using Estudarias.Application.StudentFlashcards;
using Estudarias.Domain.Identity;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;

namespace Estudarias.Application.Reviews;

/// <param name="CardId">O cartão do aluno (o <c>id</c> que a lista das revisões do dia devolve).</param>
/// <param name="Rating">A nota. Nula quando a requisição não trouxe uma das quatro, e falha como qualquer nota inválida.</param>
public sealed record ReviewFlashcardCommand(Guid CardId, ReviewRating? Rating);

/// <summary>A revisão feita: a próxima, calculada a partir do instante da revisão.</summary>
public sealed record ReviewFlashcardResult(Guid Id, DateTimeOffset NextReviewAt);

/// <summary>
/// O aluno revisa um cartão e diz como foi. Carrega o cartão <b>do aluno do token</b> (o de outro é 404, sem revelar que existe),
/// escolhe o agendador do <b>algoritmo do cartão</b>, deixa o <see cref="StudentFlashcard"/> aplicar a revisão e grava o estado novo
/// e a próxima revisão juntos. Nenhum efeito além do cartão: a revisão não muda a nota do tópico nem o desbloqueio (RN-REV-18).
/// </summary>
/// <remarks>
/// Duas revisões simultâneas do mesmo cartão: a última gravação vale, sem token de concorrência. Com a mesma nota as duas leem o mesmo
/// estado e gravam o mesmo resultado (o SM-2 é determinístico), e com notas diferentes vale a última, sem dano (PEND-REV-02).
/// </remarks>
public sealed class ReviewFlashcardHandler(
    IUserRepository users,
    IStudentFlashcardRepository studentFlashcards,
    IReviewSchedulers schedulers,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">A conta não existe, está desabilitada ou não tem aluno.</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainValidationException">A nota não é uma das quatro (<c>studentFlashcard.rating.invalid</c>).</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainNotFoundException">O cartão não existe, ou é de outro aluno (<c>review.card.notFound</c>).</exception>
    /// <exception cref="InvalidOperationException">O algoritmo do cartão não tem agendador, ou o estado gravado está ilegível: defeito do servidor.</exception>
    public async Task<ReviewFlashcardResult> Handle(
        Guid userId, ReviewFlashcardCommand command, CancellationToken cancellationToken)
    {
        // A nota vem antes de qualquer leitura: uma nota inválida falha sem tocar o banco.
        var rating = command.Rating is { } given && Enum.IsDefined(given)
            ? given
            : throw StudentFlashcardErrors.InvalidRating();

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is not { IsActive: true, StudentId: { } studentId })
        {
            throw IdentityErrors.InvalidSession();
        }

        var card = await studentFlashcards.FindAsync(command.CardId, studentId, cancellationToken)
            ?? throw ReviewErrors.NoSuchCard();

        card.Review(rating, schedulers.For(card.Algorithm), clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReviewFlashcardResult(card.Id, card.NextReviewAt);
    }
}
