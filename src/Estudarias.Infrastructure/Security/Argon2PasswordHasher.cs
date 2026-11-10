using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Isopoh.Cryptography.Argon2;
using Microsoft.Extensions.Options;

namespace Estudarias.Infrastructure.Security;

/// <summary>
/// Argon2id com o pepper como segredo próprio do algoritmo (o K da RFC 9106), não colado à senha. O pepper fica fora do texto
/// PHC, então um dump do banco sozinho não pode ser atacado offline. O sal é por senha e público; o pepper é global e secreto:
/// um não substitui o outro.
/// </summary>
internal sealed class Argon2PasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    // O mínimo da OWASP para Argon2id: 19 MiB, 2 passadas, 1 faixa.
    private const int MemoryCostKib = 19456;
    private const int TimeCost = 2;
    private const int Parallelism = 1;
    private const int HashLengthBytes = 32;

    private readonly Lazy<PasswordHash> _decoy = new(
        () => Derive(Password.Create("Throwaway-hash-1!"), options.Value.CurrentPepperVersion, options.Value));

    public PasswordHash Hash(Password password) =>
        Derive(password, options.Value.CurrentPepperVersion, options.Value);

    public bool Verify(PasswordHash? hash, string password)
    {
        if (hash is null)
        {
            // Mesmo trabalho de uma conferência real, então um usuário desconhecido custa o mesmo tempo que uma senha errada.
            _ = Verify(_decoy.Value, password);
            return false;
        }

        // Uma versão ausente da configuração é um erro de configuração (um pepper aposentado cedo demais), não uma senha
        // errada: responder falso mostraria a todo usuário afetado "senha errada" em silêncio.
        if (!options.Value.Peppers.TryGetValue(hash.PepperVersion, out var pepper))
        {
            throw new InvalidOperationException(
                $"Pepper version {hash.PepperVersion} is not configured; existing hashes cannot be verified.");
        }

        return Argon2.Verify(hash.Value, password, pepper);
    }

    public bool NeedsRehash(PasswordHash hash) => hash.PepperVersion != options.Value.CurrentPepperVersion;

    private static PasswordHash Derive(Password password, int version, PasswordHashingOptions settings) =>
        PasswordHash.Create(
            Argon2.Hash(
                password: password.Value,
                secret: settings.Peppers[version],
                timeCost: TimeCost,
                memoryCost: MemoryCostKib,
                parallelism: Parallelism,
                type: Argon2Type.HybridAddressing, // HybridAddressing é o argon2id
                hashLength: HashLengthBytes),
            version);
}
