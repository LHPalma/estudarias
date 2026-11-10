using Estudarias.Domain.Identity;
using Estudarias.Infrastructure.Security;
using Estudarias.Infrastructure.UnitTests.Support;
using Microsoft.Extensions.Options;

namespace Estudarias.Infrastructure.UnitTests.Security;

public class Argon2PasswordHasherTests
{
    private static readonly Password Secret = Password.Create("Senha-valida-1!");

    private static Argon2PasswordHasher HasherWith(int current, params (int Version, string Pepper)[] peppers) =>
        new(Options.Create(new PasswordHashingOptions
        {
            CurrentPepperVersion = current,
            Peppers = peppers.ToDictionary(p => p.Version, p => p.Pepper),
        }));

    [Fact(DisplayName = "O hash é argon2id, guarda a versão do pepper e a senha certa confere")]
    public void Hash_ThenVerify_AcceptsTheRightPassword()
    {
        // Arrange
        var hasher = HasherWith(1, (1, TestSecrets.Pepper));

        // Act
        var hash = hasher.Hash(Secret);

        // Assert
        Assert.StartsWith("$argon2id$", hash.Value, StringComparison.Ordinal);
        Assert.Equal(1, hash.PepperVersion);
        Assert.True(hasher.Verify(hash, "Senha-valida-1!"));
    }

    [Fact(DisplayName = "O custo é o mínimo da OWASP para Argon2id: 19 MiB, 2 passes e 1 faixa")]
    public void Hash_UsesTheOwaspMinimumCost()
    {
        // Act
        var hash = HasherWith(1, (1, TestSecrets.Pepper)).Hash(Secret);

        // Assert
        Assert.Contains("m=19456,t=2,p=1", hash.Value, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Senha errada não confere")]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var hasher = HasherWith(1, (1, TestSecrets.Pepper));
        var hash = hasher.Hash(Secret);

        // Act & Assert
        Assert.False(hasher.Verify(hash, "Outra-senha-2@"));
    }

    [Fact(DisplayName = "O pepper não aparece no hash e, sem o pepper certo, a senha certa não confere")]
    public void Verify_WithAnotherPepper_ReturnsFalseAndThePepperIsNotInTheHash()
    {
        // Arrange
        var hash = HasherWith(1, (1, TestSecrets.Pepper)).Hash(Secret);
        var attacker = HasherWith(1, (1, TestSecrets.OtherPepper));

        // Act & Assert
        Assert.DoesNotContain(TestSecrets.Pepper, hash.Value, StringComparison.Ordinal);
        Assert.False(attacker.Verify(hash, "Senha-valida-1!"));
    }

    [Fact(DisplayName = "A mesma senha gera hashes diferentes, por causa do sal")]
    public void Hash_TwiceTheSamePassword_DiffersBySalt()
    {
        // Arrange
        var hasher = HasherWith(1, (1, TestSecrets.Pepper));

        // Act & Assert
        Assert.NotEqual(hasher.Hash(Secret).Value, hasher.Hash(Secret).Value);
    }

    [Fact(DisplayName = "Sem hash (usuário inexistente) a verificação devolve falso, sem lançar")]
    public void Verify_WithNullHash_ReturnsFalse()
    {
        // Arrange
        var hasher = HasherWith(1, (1, TestSecrets.Pepper));

        // Act & Assert
        Assert.False(hasher.Verify(null, "Senha-valida-1!"));
    }

    [Fact(DisplayName = "Depois de rotacionar o pepper, o hash antigo ainda confere e pede para ser refeito")]
    public void Verify_AfterRotation_StillAcceptsTheOldHashAndAsksForRehash()
    {
        // Arrange
        var before = HasherWith(1, (1, TestSecrets.Pepper));
        var oldHash = before.Hash(Secret);
        var after = HasherWith(2, (1, TestSecrets.Pepper), (2, TestSecrets.OtherPepper));

        // Act
        var newHash = after.Hash(Secret);

        // Assert
        Assert.True(after.Verify(oldHash, "Senha-valida-1!"));
        Assert.True(after.NeedsRehash(oldHash));
        Assert.Equal(2, newHash.PepperVersion);
        Assert.False(after.NeedsRehash(newHash));
        Assert.True(after.Verify(newHash, "Senha-valida-1!"));
    }

    [Fact(DisplayName = "Versão de pepper que saiu da configuração é erro de configuração, não senha errada")]
    public void Verify_WithRetiredPepperVersion_Throws()
    {
        // Arrange
        var hash = HasherWith(1, (1, TestSecrets.Pepper)).Hash(Secret);
        var retired = HasherWith(2, (2, TestSecrets.OtherPepper));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => retired.Verify(hash, "Senha-valida-1!"));
        Assert.Contains("Pepper version 1", exception.Message, StringComparison.Ordinal);
    }
}
