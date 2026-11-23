using Estudarias.Domain.StudentFlashcards;

namespace Estudarias.Application.StudentFlashcards;

/// <summary>A escrita dos cartões de revisão do aluno: criar o cartão pelo erro e revisá-lo.</summary>
public interface IStudentFlashcardRepository
{
    /// <summary>
    /// O cartão <paramref name="id"/>, rastreado para a revisão, só se for do aluno <paramref name="studentId"/>. O cartão de outro
    /// aluno volta nulo, como um que não existe: quem pede não fica sabendo que ele existe.
    /// </summary>
    Task<StudentFlashcard?> FindAsync(Guid id, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Se o aluno já tem o cartão daquele flashcard: o erro repetido não cria outro (RN-REV-12).</summary>
    Task<bool> ExistsAsync(Guid studentId, Guid flashcardId, CancellationToken cancellationToken);

    /// <summary>Marca o cartão para ser gravado; só vale depois de <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(StudentFlashcard card);

    /// <summary>
    /// Tira da gravação um cartão que foi marcado com <see cref="Add"/> e ainda não foi gravado. Serve à corrida em que outra requisição
    /// cria o mesmo cartão antes: a perdedora descarta o dela e grava o resto, sem perder a tentativa (FA-RSP-10).
    /// </summary>
    void Discard(StudentFlashcard card);
}
