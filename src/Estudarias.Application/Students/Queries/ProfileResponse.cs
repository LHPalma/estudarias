using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;

namespace Estudarias.Application.Students.Queries;

/// <summary>O perfil de quem está logado: a conta de acesso e o perfil de estudo a que ela está ligada.</summary>
public sealed record ProfileResponse(ProfileUserResponse User, ProfileStudentResponse Student);

/// <summary>A conta de acesso. Só o que o app precisa: o hash da senha nunca sai do banco numa leitura.</summary>
public sealed record ProfileUserResponse(Guid Id, string Email, UserRole Role);

/// <summary>O perfil de estudo. O progresso por tópico fica de fora até o <c>student_topic</c> ser mapeado.</summary>
public sealed record ProfileStudentResponse(Guid Id, string Name, ExperimentalGroup ExperimentalGroup);
