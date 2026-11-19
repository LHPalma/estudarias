using Estudarias.Domain.Students;

namespace Estudarias.Application.Students;

public interface IStudentRepository
{
    /// <summary>
    /// O aluno com todo o progresso por tópico, rastreado para a escrita: o caso de uso desbloqueia ou marca o domínio, e a
    /// <c>IUnitOfWork</c> grava. Nulo quando o aluno não existe.
    /// </summary>
    Task<Student?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marca o aluno para ser gravado; só vale depois de <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(Student student);
}
