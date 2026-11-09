using System.Security.Cryptography;
using System.Text;
using Estudarias.Application.Identity;

namespace Estudarias.Application.UnitTests.Identity;

public class RefreshTokenSecretTests
{
    [Fact(DisplayName = "O token cru tem 32 bytes em hexadecimal minúsculo")]
    public void Generate_ReturnsA64CharLowercaseHexToken()
    {
        // Act
        var (raw, _) = RefreshTokenSecret.Generate();

        // Assert
        Assert.Equal(64, raw.Length);
        Assert.Matches("^[0-9a-f]{64}$", raw);
    }

    [Fact(DisplayName = "O hash guardado é o SHA-256 do token cru, e não o próprio token")]
    public void Generate_ReturnsTheSha256OfTheRawToken()
    {
        // Act
        var (raw, hash) = RefreshTokenSecret.Generate();

        // Assert
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw))), hash);
        Assert.NotEqual(raw, hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact(DisplayName = "Cada token gerado é diferente")]
    public void Generate_TwiceReturnsDifferentTokens()
    {
        // Act
        var first = RefreshTokenSecret.Generate();
        var second = RefreshTokenSecret.Generate();

        // Assert
        Assert.NotEqual(first.Raw, second.Raw);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact(DisplayName = "O mesmo token cru dá sempre o mesmo hash")]
    public void Hash_IsDeterministic()
    {
        // Act & Assert
        Assert.Equal(RefreshTokenSecret.Hash("abc"), RefreshTokenSecret.Hash("abc"));
        Assert.NotEqual(RefreshTokenSecret.Hash("abc"), RefreshTokenSecret.Hash("abd"));
    }
}
