using Estudarias.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Students.Queries;

public sealed class ProfileReads(IApplicationDbContext db) : IProfileReads
{
    public async Task<ProfileResponse?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Uma consulta só (a conta e o aluno ligado a ela), e só as colunas do contrato: o User tem o hash da senha, que nunca deve
        // sair do banco numa leitura, e o Student não é carregado como agregado (o progresso dele ainda nem está mapeado).
        // Materializa aqui, no ponto de leitura, porque o EF Core só traduz a projeção quando a consulta termina.
        return await (
            from user in db.Users
            join student in db.Students on user.StudentId equals student.Id
            where user.Id == userId && user.IsActive
            select new ProfileResponse(
                new ProfileUserResponse(user.Id, user.Email, user.Role),
                new ProfileStudentResponse(student.Id, student.Name, student.Group)))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
