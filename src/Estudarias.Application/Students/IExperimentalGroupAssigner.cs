using Estudarias.Domain.Students;

namespace Estudarias.Application.Students;

/// <summary>
/// Decide em qual grupo do experimento um aluno novo entra. É uma porta para trocar a estratégia (sorteio, alternância, grupo
/// fixo) sem mexer no cadastro.
/// </summary>
public interface IExperimentalGroupAssigner
{
    ExperimentalGroup Assign();
}
