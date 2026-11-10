using System.Security.Cryptography;
using System.Text;

namespace Estudarias.Application.Identity;

/// <summary>
/// Gera e calcula o hash do refresh token: uma sequência aleatória com muita entropia, não uma senha que uma pessoa escolheu, então
/// um hash rápido (SHA-256) basta. Só o hash é guardado; o valor cru existe uma vez, na resposta.
/// </summary>
internal static class RefreshTokenSecret
{
    private const int TokenSizeBytes = 32;

    public static (string Raw, string Hash) Generate()
    {
        var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenSizeBytes));
        return (raw, Hash(raw));
    }

    public static string Hash(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
