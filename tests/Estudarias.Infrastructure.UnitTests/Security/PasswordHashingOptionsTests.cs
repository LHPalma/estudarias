using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Estudarias.Infrastructure.Security;
using Estudarias.Infrastructure.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Estudarias.Infrastructure.UnitTests.Security;

public class PasswordHashingOptionsTests
{
    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:Default"] = "Host=unused";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }

    private static PasswordHashingOptions Resolve(Dictionary<string, string?> settings) =>
        Provider(settings).GetRequiredService<IOptions<PasswordHashingOptions>>().Value;

    [Fact(DisplayName = "Configuração completa de pepper passa na validação")]
    public void Value_WithValidSettings_Passes()
    {
        // Act
        var options = Resolve(new()
        {
            ["PasswordHashing:CurrentPepperVersion"] = "1",
            ["PasswordHashing:Peppers:1"] = TestSecrets.Pepper,
        });

        // Assert
        Assert.Equal(1, options.CurrentPepperVersion);
        Assert.Equal(TestSecrets.Pepper, options.Peppers[1]);
    }

    [Fact(DisplayName = "Durante uma rotação, dois peppers configurados passam na validação")]
    public void Value_WithTwoPeppersDuringRotation_Passes()
    {
        // Act
        var options = Resolve(new()
        {
            ["PasswordHashing:CurrentPepperVersion"] = "2",
            ["PasswordHashing:Peppers:1"] = TestSecrets.Pepper,
            ["PasswordHashing:Peppers:2"] = TestSecrets.OtherPepper,
        });

        // Assert
        Assert.Equal(2, options.CurrentPepperVersion);
        Assert.Equal(2, options.Peppers.Count);
    }

    [Fact(DisplayName = "Sem nenhum pepper a configuração é recusada")]
    public void Value_WithoutPeppers_Fails()
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(
            () => Resolve(new() { ["PasswordHashing:CurrentPepperVersion"] = "1" }));
        Assert.Contains("Peppers is not configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Versão corrente fora do mapa de peppers é recusada")]
    public void Value_WithCurrentVersionMissing_Fails()
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve(new()
        {
            ["PasswordHashing:CurrentPepperVersion"] = "2",
            ["PasswordHashing:Peppers:1"] = TestSecrets.Pepper,
        }));
        Assert.Contains("CurrentPepperVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Pepper curto é recusado, inclusive o de uma versão antiga")]
    public void Value_WithShortPepper_FailsEvenForAnOldVersion()
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve(new()
        {
            ["PasswordHashing:CurrentPepperVersion"] = "2",
            ["PasswordHashing:Peppers:1"] = "curto",
            ["PasswordHashing:Peppers:2"] = TestSecrets.Pepper,
        }));
        Assert.Contains("at least 32 characters", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AddInfrastructure registra o IPasswordHasher, pronto para uso com a configuração")]
    public void AddInfrastructure_RegistersAWorkingPasswordHasher()
    {
        // Arrange
        using var provider = Provider(new()
        {
            ["PasswordHashing:CurrentPepperVersion"] = "1",
            ["PasswordHashing:Peppers:1"] = TestSecrets.Pepper,
        });

        // Act
        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var hash = hasher.Hash(Password.Create("Senha-valida-1!"));

        // Assert
        Assert.True(hasher.Verify(hash, "Senha-valida-1!"));
        Assert.Same(hasher, provider.GetRequiredService<IPasswordHasher>());
    }
}
