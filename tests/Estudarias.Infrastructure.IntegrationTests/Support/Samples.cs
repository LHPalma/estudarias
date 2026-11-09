using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

internal static class Samples
{
    public static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public static PasswordHash Hash(int pepperVersion = 1) =>
        PasswordHash.Create("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA", pepperVersion);

    public static Student Student(ExperimentalGroup group = ExperimentalGroup.Diagnostic) =>
        Domain.Students.Student.Create("Ana", group);

    public static User User(Student student, string email = "ana@example.com", int pepperVersion = 1) =>
        Domain.Identity.User.RegisterStudent(email, Hash(pepperVersion), student.Id);

    /// <summary>Um hash de token de 64 caracteres, como o SHA-256 em hexadecimal; o caractere distingue um do outro.</summary>
    public static string TokenHash(char marker) => new(marker, 64);

    public static RefreshToken Token(Guid userId, Guid familyId, char marker, DateTimeOffset? issuedAt = null) =>
        RefreshToken.Issue(userId, familyId, TokenHash(marker), issuedAt ?? Now, RefreshLifetime);
}
