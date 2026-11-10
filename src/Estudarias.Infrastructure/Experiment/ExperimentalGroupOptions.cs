using Estudarias.Domain.Students;

namespace Estudarias.Infrastructure.Experiment;

public sealed class ExperimentalGroupOptions
{
    public const string SectionName = "ExperimentalGroup";

    /// <summary>
    /// A feature flag do sorteio. Ligada, todo aluno novo cai no grupo de controle ou no de diagnóstico com 50% de chance cada.
    /// Desligada, todo aluno novo cai em <see cref="FallbackGroup"/>, sem sorteio: serve para o período em que o experimento ainda
    /// não começou ou já terminou. Não afeta os alunos que já têm grupo.
    /// </summary>
    public bool RandomAssignment { get; init; } = true;

    /// <summary>O grupo de todo aluno novo enquanto o sorteio está desligado. O padrão é o diagnóstico, que é o app completo.</summary>
    public ExperimentalGroup FallbackGroup { get; init; } = ExperimentalGroup.Diagnostic;
}
