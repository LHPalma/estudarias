using System.Diagnostics.CodeAnalysis;

namespace Estudarias.Domain.Identity;

/// <summary>
/// Uma senha derivada em formato PHC mais a versão do pepper que a derivou: os dois são necessários para verificá-la,
/// então viajam como um valor só. O domínio nunca vê a senha em texto puro, só este envelope.
/// </summary>
public sealed record PasswordHash
{
    private const int MinimumDollarSigns = 5;

    private PasswordHash(string value, int pepperVersion)
    {
        Value = value;
        PepperVersion = pepperVersion;
    }

    public string Value { get; }

    public int PepperVersion { get; }

    /// <summary>Monta a partir de um texto PHC e da versão do pepper que o derivou; lança quando não é válido.</summary>
    public static PasswordHash Create(string? input, int pepperVersion) =>
        TryCreate(input, pepperVersion, out var hash) ? hash : throw IdentityErrors.InvalidPasswordHash();

    /// <summary>Devolve falso em vez de lançar quando a entrada não é PHC ou a versão é menor que 1.</summary>
    public static bool TryCreate(string? input, int pepperVersion, [NotNullWhen(true)] out PasswordHash? hash)
    {
        hash = null;

        // A versão zero é exatamente o que uma propriedade esquecida sem atribuição carregaria.
        if (pepperVersion < 1)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('$') || input.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (input.Count(c => c == '$') < MinimumDollarSigns)
        {
            return false;
        }

        hash = new PasswordHash(input, pepperVersion);
        return true;
    }

    public override string ToString() => "PasswordHash(hidden)";
}
