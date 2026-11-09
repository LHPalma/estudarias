namespace Estudarias.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 pede uma chave de pelo menos 256 bits; menos que isso enfraquece a assinatura.</summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>
    /// A chave que assina e confere o access token. É o segredo mais sensível do módulo: com ela se forja um token de qualquer
    /// aluno. Fica fora do repositório e do banco.
    /// </summary>
    public string SigningKey { get; init; } = "";

    public string Issuer { get; init; } = "Estudarias";

    public string Audience { get; init; } = "Estudarias";

    /// <summary>A vida do access token. Curta de propósito: ele não guarda estado e não pode ser revogado antes de vencer.</summary>
    public int AccessTokenMinutes { get; init; } = 15;
}
