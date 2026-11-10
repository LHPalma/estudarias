using Estudarias.Domain.Common;

namespace Estudarias.Domain.Identity;

/// <summary>
/// A conta de acesso. Separada do <c>Student</c> de propósito: a conta guarda a identidade (e-mail e senha), o aluno guarda
/// o perfil de estudo e o progresso, e o <see cref="StudentId"/> liga os dois.
/// </summary>
public sealed class User : BaseEntity, IAggregateRoot
{
    public const int MaxEmailLength = 254;

    // Só o EF Core usa: ele não consegue ligar PasswordHash, que é uma propriedade complexa, a um parâmetro do construtor.
    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    private User(string email, PasswordHash passwordHash, UserRole role, Guid? studentId)
    {
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        StudentId = studentId;
        IsActive = true;
    }

    /// <summary>O e-mail sem espaços nas pontas e em minúsculas; é o que o usuário usa para entrar.</summary>
    public string Email { get; }

    public PasswordHash PasswordHash { get; private set; }

    public UserRole Role { get; }

    /// <summary>O perfil de estudo ligado a esta conta; todo usuário com o papel aluno tem um.</summary>
    public Guid? StudentId { get; }

    /// <summary>Um usuário desabilitado não consegue entrar nem renovar a sessão.</summary>
    public bool IsActive { get; }

    /// <summary>Cria a conta de um aluno que se cadastrou sozinho.</summary>
    public static User RegisterStudent(string email, PasswordHash passwordHash, Guid studentId) =>
        new(NormalizeEmail(email), passwordHash, UserRole.Student, studentId);

    /// <summary>A forma em que um e-mail é guardado e procurado; lança quando não é um endereço.</summary>
    public static string NormalizeEmail(string? email) =>
        TryNormalizeEmail(email, out var normalized) ? normalized : throw IdentityErrors.InvalidEmail();

    /// <summary>Devolve falso em vez de lançar quando o texto não é um endereço.</summary>
    public static bool TryNormalizeEmail(string? email, out string normalized)
    {
        normalized = email?.Trim().ToLowerInvariant() ?? "";

        return normalized.Length is > 0 and <= MaxEmailLength
            && !normalized.Any(char.IsWhiteSpace)
            && normalized.Count(c => c == '@') == 1
            && !normalized.StartsWith('@')
            && !normalized.EndsWith('@')
            && normalized[(normalized.IndexOf('@') + 1)..].Contains('.');
    }

    /// <summary>Troca o hash por um equivalente, como quando o login descobre que ele usa um pepper antigo.</summary>
    public void Rehash(PasswordHash passwordHash) => PasswordHash = passwordHash;
}
