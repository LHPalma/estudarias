using Estudarias.Api.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Estudarias.Api.UnitTests.Cors;

public class CorsSettingsTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Estudarias.Api";

        public string ContentRootPath { get; set; } = "";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static CorsSettings Resolve(Dictionary<string, string?> settings, string environment = "Production")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddCorsPolicy(configuration, new Environment(environment))
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<CorsSettings>>().Value;
    }

    [Theory(DisplayName = "Uma origem com esquema, host e porta opcional é válida")]
    [InlineData("http://localhost:8081")]
    [InlineData("https://app.estudarias.com.br")]
    [InlineData("HTTP://LOCALHOST:19006")]
    [InlineData("http://192.168.0.10:8081")]
    public void IsValidOrigin_WithSchemeAndHost_IsTrue(string origin)
    {
        // Act & Assert
        Assert.True(CorsSettings.IsValidOrigin(origin));
    }

    [Theory(DisplayName = "Curinga, barra no fim, caminho, esquema estranho e texto vazio não são origem")]
    [InlineData("*")]
    [InlineData("http://localhost:8081/")]
    [InlineData("http://localhost:8081/app")]
    [InlineData("http://localhost:8081?x=1")]
    [InlineData("ftp://localhost")]
    [InlineData("localhost:8081")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsValidOrigin_WithAnythingElse_IsFalse(string? origin)
    {
        // Act & Assert
        Assert.False(CorsSettings.IsValidOrigin(origin));
    }

    [Fact(DisplayName = "Sem a seção, nenhuma origem é liberada")]
    public void Value_WithoutTheSection_AllowsNoOrigin()
    {
        // Act
        var settings = Resolve([]);

        // Assert
        Assert.Empty(settings.AllowedOrigins);
    }

    [Fact(DisplayName = "As origens configuradas são lidas na ordem")]
    public void Value_ReadsTheConfiguredOrigins()
    {
        // Act
        var settings = Resolve(new()
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:8081",
            ["Cors:AllowedOrigins:1"] = "https://app.estudarias.com.br",
        });

        // Assert
        Assert.Equal(["http://localhost:8081", "https://app.estudarias.com.br"], settings.AllowedOrigins);
    }

    [Theory(DisplayName = "Uma origem inválida na configuração derruba a leitura das opções, em vez de nunca bater")]
    [InlineData("*")]
    [InlineData("http://localhost:8081/")]
    public void Value_WithAnInvalidOrigin_Throws(string origin)
    {
        // Act & Assert
        Assert.Throws<OptionsValidationException>(() => Resolve(new() { ["Cors:AllowedOrigins:0"] = origin }));
    }
}
