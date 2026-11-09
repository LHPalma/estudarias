using System.Reflection;
using Estudarias.Domain.Identity;

namespace Estudarias.Application.UnitTests.Support;

internal static class TestUsers
{
    public static readonly PasswordHash Hash =
        PasswordHash.Create("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA", 1);

    public static User Active(string email = "ana@example.com", PasswordHash? hash = null) =>
        User.RegisterStudent(email, hash ?? Hash, Guid.CreateVersion7());

    /// <summary>
    /// O domínio ainda não tem como desabilitar uma conta (não há quem faça isso), mas o login e a renovação precisam recusar uma.
    /// O teste marca o campo como o EF faz ao carregar uma conta já desabilitada do banco.
    /// </summary>
    public static User Inactive(string email = "ana@example.com")
    {
        var user = Active(email);
        typeof(User)
            .GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(user, false);
        return user;
    }
}
