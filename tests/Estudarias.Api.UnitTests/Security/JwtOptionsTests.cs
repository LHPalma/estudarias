using Estudarias.Api.Security;
using Estudarias.Api.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Estudarias.Api.UnitTests.Security;

public class JwtOptionsTests
{
    private static JwtOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSecurity(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<JwtOptions>>().Value;
    }

    [Fact(DisplayName = "Uma chave com 32 caracteres ou mais passa, e os demais valores têm padrão")]
    public void Value_WithAValidKey_PassesWithDefaults()
    {
        // Act
        var options = Resolve(new() { ["Jwt:SigningKey"] = TestSecrets.SigningKey });

        // Assert
        Assert.Equal(TestSecrets.SigningKey, options.SigningKey);
        Assert.Equal("Estudarias", options.Issuer);
        Assert.Equal("Estudarias", options.Audience);
        Assert.Equal(15, options.AccessTokenMinutes);
    }

    [Fact(DisplayName = "Emissor, audiência e vida do token vêm da configuração")]
    public void Value_BindsTheConfiguration()
    {
        // Act
        var options = Resolve(new()
        {
            ["Jwt:SigningKey"] = TestSecrets.SigningKey,
            ["Jwt:Issuer"] = "Quem",
            ["Jwt:Audience"] = "Para quem",
            ["Jwt:AccessTokenMinutes"] = "5",
        });

        // Assert
        Assert.Equal("Quem", options.Issuer);
        Assert.Equal("Para quem", options.Audience);
        Assert.Equal(5, options.AccessTokenMinutes);
    }

    [Fact(DisplayName = "RNF-03: uma chave de exatamente 32 caracteres passa e uma de 31 é recusada")]
    public void Value_AtTheKeyLengthBoundary()
    {
        // Act & Assert
        Assert.Equal(32, Resolve(new() { ["Jwt:SigningKey"] = new string('k', 32) }).SigningKey.Length);
        var exception = Assert.Throws<OptionsValidationException>(
            () => Resolve(new() { ["Jwt:SigningKey"] = new string('k', 31) }));
        Assert.Contains("Jwt:SigningKey needs at least 32 characters", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "RNF-03: sem a chave a configuração é recusada")]
    public void Value_WithoutTheKey_Fails()
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve([]));
        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "RNF-03: uma vida de token que não é positiva é recusada")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Value_WithANonPositiveLifetime_Fails(string minutes)
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve(new()
        {
            ["Jwt:SigningKey"] = TestSecrets.SigningKey,
            ["Jwt:AccessTokenMinutes"] = minutes,
        }));
        Assert.Contains("AccessTokenMinutes must be positive", exception.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "RNF-03: emissor ou audiência em branco é recusado")]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    public void Value_WithABlankIssuerOrAudience_Fails(string key)
    {
        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve(new()
        {
            ["Jwt:SigningKey"] = TestSecrets.SigningKey,
            [key] = "   ",
        }));
        Assert.Contains("must not be blank", exception.Message, StringComparison.Ordinal);
    }
}
