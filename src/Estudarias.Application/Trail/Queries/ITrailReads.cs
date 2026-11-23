namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// Superfície de leitura da trilha do aluno logado. Leitura não passa por caso de uso nem por repositório (ADR-002); a liberação dos
/// tópicos iniciais, que é escrita, vem antes, pelo <see cref="UnlockInitialTopicsHandler"/>.
/// </summary>
public interface ITrailReads
{
    /// <summary>
    /// A trilha do aluno da conta, ou nulo quando a conta não existe, está desabilitada ou não tem aluno ligado. O chamador não
    /// distingue os três casos: para o cliente, a sessão deixou de valer.
    /// </summary>
    Task<TrailView?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
}
