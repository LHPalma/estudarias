using Estudarias.Domain.Students;

namespace Estudarias.Application.Students;

public interface IStudentRepository
{
    /// <summary>Marca o aluno para ser gravado; só vale depois de <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(Student student);
}
