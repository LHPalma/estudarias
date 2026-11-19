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
}
