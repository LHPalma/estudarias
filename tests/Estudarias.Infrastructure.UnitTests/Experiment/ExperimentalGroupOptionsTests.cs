using Estudarias.Application.Students;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.Experiment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Estudarias.Infrastructure.UnitTests.Experiment;

public class ExperimentalGroupOptionsTests
{
    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:Default"] = "Host=unused";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }

    private static ExperimentalGroupOptions Resolve(Dictionary<string, string?> settings) =>
        Provider(settings).GetRequiredService<IOptionsMonitor<ExperimentalGroupOptions>>().CurrentValue;

    [Fact(DisplayName = "Sem a seção na configuração, o sorteio fica ligado e a reserva é o diagnóstico")]
    public void CurrentValue_WithoutTheSection_UsesTheDefaults()
    {
        // Act
        var options = Resolve([]);

        // Assert
        Assert.True(options.RandomAssignment);
        Assert.Equal(ExperimentalGroup.Diagnostic, options.FallbackGroup);
    }

    [Fact(DisplayName = "A flag e o grupo de reserva vêm da configuração, pelo nome do grupo")]
    public void CurrentValue_BindsTheSection()
    {
        // Act
        var options = Resolve(new()
        {
            ["ExperimentalGroup:RandomAssignment"] = "false",
            ["ExperimentalGroup:FallbackGroup"] = "Control",
        });

        // Assert
        Assert.False(options.RandomAssignment);
        Assert.Equal(ExperimentalGroup.Control, options.FallbackGroup);
    }

    [Fact(DisplayName = "Um grupo de reserva que não existe é recusado")]
    public void CurrentValue_WithUndefinedFallbackGroup_Fails()
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(
            () => Resolve(new() { ["ExperimentalGroup:FallbackGroup"] = "7" }));
        Assert.Contains("FallbackGroup", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AddInfrastructure registra o sorteio, e a flag desligada chega até ele")]
    public void AddInfrastructure_RegistersTheAssignerWiredToTheFlag()
    {
        // Arrange
        using var provider = Provider(new()
        {
            ["ExperimentalGroup:RandomAssignment"] = "false",
            ["ExperimentalGroup:FallbackGroup"] = "Control",
        });

        // Act
        var assigner = provider.GetRequiredService<IExperimentalGroupAssigner>();

        // Assert
        Assert.Equal(ExperimentalGroup.Control, assigner.Assign());
        Assert.Same(assigner, provider.GetRequiredService<IExperimentalGroupAssigner>());
    }
}
