using Estudarias.Application.Students;
using Estudarias.Domain.Students;
using Microsoft.Extensions.Options;

namespace Estudarias.Infrastructure.Experiment;

/// <summary>
/// Sorteia o grupo do aluno novo, 50/50, atrás da flag <see cref="ExperimentalGroupOptions.RandomAssignment"/>. Lê o valor atual da
/// configuração a cada chamada, então virar a flag não pede reinício quando a configuração vem de um arquivo que recarrega.
/// </summary>
internal sealed class ExperimentalGroupAssigner(IOptionsMonitor<ExperimentalGroupOptions> options, Random random)
    : IExperimentalGroupAssigner
{
    public ExperimentalGroup Assign()
    {
        var current = options.CurrentValue;

        if (!current.RandomAssignment)
        {
            return current.FallbackGroup;
        }

        return random.Next(2) == 0 ? ExperimentalGroup.Control : ExperimentalGroup.Diagnostic;
    }
}
