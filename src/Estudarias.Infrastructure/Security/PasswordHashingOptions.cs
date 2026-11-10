namespace Estudarias.Infrastructure.Security;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    /// <summary>Menor tamanho aceito para um pepper: um segredo curto cairia em ataque de força bruta.</summary>
    public const int MinimumPepperLength = 32;

    /// <summary>A versão com que os hashes novos são derivados.</summary>
    public int CurrentPepperVersion { get; init; }

    /// <summary>Todo pepper que ainda tem um hash para verificar, por versão. Um antigo só sai quando nenhum hash o usa.</summary>
    public Dictionary<int, string> Peppers { get; init; } = [];
}
