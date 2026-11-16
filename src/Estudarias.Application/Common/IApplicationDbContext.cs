using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;

namespace Estudarias.Application.Common;

/// <summary>
/// O banco visto só para leitura: cada agregado como <see cref="IQueryable{T}"/>, sem <c>SaveChanges</c>, <c>Add</c> nem rastreamento.
/// As consultas (as classes <c>*Reads</c>) montam o LINQ por cima e materializam o resultado. A escrita não passa por aqui: segue
/// pelos repositórios e pela <see cref="IUnitOfWork"/> (veja o ADR-002).
/// </summary>
public interface IApplicationDbContext
{
    IQueryable<Student> Students { get; }

    IQueryable<User> Users { get; }
}
