using Estudarias.Application.Students.Queries;

namespace Estudarias.Api.Contracts.Me;

/// <summary>A conta de acesso de quem está logado.</summary>
public sealed record MeUserResponse(Guid Id, string Email, string Role);

/// <summary>O perfil de estudo de quem está logado. <c>ExperimentalGroup</c> diz ao app qual versão da experiência mostrar.</summary>
public sealed record MeStudentResponse(Guid Id, string Name, string ExperimentalGroup);

/// <summary>
/// O perfil de quem está logado: a conta (<c>user</c>) e o aluno ligado a ela (<c>student</c>), separados como no domínio. O
/// <c>student.id</c> é o <c>studentId</c> que o login devolve. O progresso por tópico ainda não faz parte da resposta.
/// </summary>
public sealed record MeResponse(MeUserResponse User, MeStudentResponse Student)
{
    public static MeResponse From(ProfileResponse profile) =>
        new(
            new MeUserResponse(profile.User.Id, profile.User.Email, profile.User.Role.ToString()),
            new MeStudentResponse(profile.Student.Id, profile.Student.Name, profile.Student.ExperimentalGroup.ToString()));
}
