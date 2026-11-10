namespace Estudarias.Application.Students.Queries;

/// <summary>
/// Superfície de leitura do perfil de quem está logado. Leitura não passa por caso de uso nem por repositório: o repositório é o
/// agregado na escrita, e um caso de uso que só repassa seria uma camada morta (ADR-002).
/// </summary>
public interface IProfileReads
{
    /// <summary>
    /// O perfil da conta, ou nulo quando a conta não existe, está desabilitada ou não tem aluno ligado. O chamador não distingue
    /// os três casos: para o cliente, a sessão deixou de valer.
    /// </summary>
    Task<ProfileResponse?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
}
