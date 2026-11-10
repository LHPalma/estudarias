using Estudarias.Domain.Students;
using Estudarias.Infrastructure.Experiment;
using Estudarias.Infrastructure.UnitTests.Support;

namespace Estudarias.Infrastructure.UnitTests.Experiment;

public class ExperimentalGroupAssignerTests
{
    private static ExperimentalGroupOptions On() => new() { RandomAssignment = true };

    [Fact(DisplayName = "Com o sorteio ligado, o sorteio decide o grupo")]
    public void Assign_WithFlagOn_UsesTheDraw()
    {
        // Arrange
        var assigner = new ExperimentalGroupAssigner(new TestOptionsMonitor<ExperimentalGroupOptions>(On()), new ScriptedRandom(0, 1));

        // Act
        var first = assigner.Assign();
        var second = assigner.Assign();

        // Assert
        Assert.Equal(ExperimentalGroup.Control, first);
        Assert.Equal(ExperimentalGroup.Diagnostic, second);
    }

    [Fact(DisplayName = "Com o sorteio ligado, os dois grupos saem em cerca de metade das vezes cada")]
    public void Assign_WithFlagOn_SplitsRoughlyFiftyFifty()
    {
        // Arrange
        var assigner = new ExperimentalGroupAssigner(new TestOptionsMonitor<ExperimentalGroupOptions>(On()), new Random(20261109));

        // Act
        var groups = Enumerable.Range(0, 10_000).Select(_ => assigner.Assign()).ToList();

        // Assert
        var control = groups.Count(group => group == ExperimentalGroup.Control);
        Assert.InRange(control, 4_800, 5_200);
        Assert.Equal(10_000 - control, groups.Count(group => group == ExperimentalGroup.Diagnostic));
    }

    [Theory(DisplayName = "Com o sorteio desligado, todo aluno novo cai no grupo de reserva, sem sortear")]
    [InlineData(ExperimentalGroup.Control)]
    [InlineData(ExperimentalGroup.Diagnostic)]
    public void Assign_WithFlagOff_ReturnsTheFallbackGroupWithoutDrawing(ExperimentalGroup fallback)
    {
        // Arrange
        var options = new ExperimentalGroupOptions { RandomAssignment = false, FallbackGroup = fallback };
        var assigner = new ExperimentalGroupAssigner(new TestOptionsMonitor<ExperimentalGroupOptions>(options), new ForbiddenRandom());

        // Act & Assert
        Assert.Equal(fallback, assigner.Assign());
        Assert.Equal(fallback, assigner.Assign());
    }

    [Fact(DisplayName = "Virar a flag vale na próxima chamada, sem recriar o sorteador")]
    public void Assign_WhenTheFlagIsToggled_FollowsTheCurrentValue()
    {
        // Arrange
        var monitor = new TestOptionsMonitor<ExperimentalGroupOptions>(
            new ExperimentalGroupOptions { RandomAssignment = false, FallbackGroup = ExperimentalGroup.Control });
        var assigner = new ExperimentalGroupAssigner(monitor, new ScriptedRandom(1));

        // Act
        var whileOff = assigner.Assign();
        monitor.CurrentValue = On();
        var whileOn = assigner.Assign();

        // Assert
        Assert.Equal(ExperimentalGroup.Control, whileOff);
        Assert.Equal(ExperimentalGroup.Diagnostic, whileOn);
    }
}
