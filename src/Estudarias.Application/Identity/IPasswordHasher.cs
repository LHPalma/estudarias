using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

/// <summary>
/// Deriva e confere senhas. A implementação escolhe o algoritmo, o custo e o pepper; quem chama não conhece nenhum deles, então
/// aumentar o custo ou rotacionar o pepper não toca em nenhum caso de uso.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Só aceita <see cref="Password"/>, que já passou pela política: nenhum hash sai de uma senha que ninguém validou.</summary>
    PasswordHash Hash(Password password);

    /// <summary>
    /// Confere a senha contra o hash. Com um hash nulo (usuário desconhecido), confere contra um descartável e devolve falso,
    /// gastando o mesmo tempo: a ausência do usuário não aparece no tempo de resposta.
    /// </summary>
    /// <exception cref="InvalidOperationException">O pepper da versão do hash não está mais configurado.</exception>
    bool Verify(PasswordHash? hash, string password);

    /// <summary>Verdadeiro quando o hash foi derivado com um pepper que já não é o atual.</summary>
    bool NeedsRehash(PasswordHash hash);
}
