using Estudarias.Application.Common;
using Estudarias.Application.Students;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;

namespace Estudarias.Application.Identity;

public sealed record RegisterStudentCommand(string Name, string Email, string Password);

/// <summary>
/// O aluno se cadastra sozinho: cria o perfil de estudo (<see cref="Student"/>, já no grupo do experimento) e a conta de acesso
/// (<see cref="User"/>) ligados por <see cref="User.StudentId"/>, e devolve uma sessão aberta, então ele já entra logado. Aluno e
/// conta são gravados juntos: se a conta falha, o aluno também não entra.
/// </summary>
public sealed class RegisterStudentHandler(
    IUserRepository users,
    IStudentRepository students,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    IExperimentalGroupAssigner groups,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<AuthPayload> Handle(RegisterStudentCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // A política vale antes de qualquer consulta ou custo: nenhum hash sai de uma senha que ninguém validou.
        var email = User.NormalizeEmail(command.Email);
        var password = Password.Create(command.Password);

        if (await users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw IdentityErrors.EmailInUse();
        }

        var student = Student.Create(command.Name, groups.Assign());
        var user = User.RegisterStudent(email, hasher.Hash(password), student.Id);
        var (refreshToken, rawRefreshToken) = SessionTokens.IssueRefreshToken(user.Id, Guid.CreateVersion7(), now);

        students.Add(student);
        users.Add(user);
        refreshTokens.Add(refreshToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // A checagem acima não fecha a janela entre duas requisições com o mesmo e-mail: só o índice único fecha, e o
            // único valor que um cadastro novo pode repetir é o e-mail (os ids e o token são aleatórios).
            throw IdentityErrors.EmailInUse();
        }

        var access = accessTokens.Issue(user);
        return new AuthPayload(access.Token, access.ExpiresAt, rawRefreshToken, user);
    }
}
