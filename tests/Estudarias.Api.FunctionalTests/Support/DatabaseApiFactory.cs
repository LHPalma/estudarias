using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Api.FunctionalTests.Support;

/// <summary>
/// A API inteira ligada a um banco de verdade (o de um teste, clonado do modelo migrado), com o relógio controlável e os endpoints
/// de teste. <paramref name="settings"/> troca a configuração: é como um teste simula outro ambiente ou uma rotação de pepper.
/// </summary>
public sealed class DatabaseApiFactory(
    string connectionString,
    string environment = "Development",
    Action<Dictionary<string, string?>>? settings = null) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Começa no instante real e fica parado: o access token que a API emite vale em relação a esse relógio, e o middleware do JWT
    /// confere a validade pelo relógio de verdade, então o teste só avança o tempo quando não vai mais usar o token.
    /// </summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Jwt:SigningKey"] = ApiFactory.SigningKey,
        };
        settings?.Invoke(values);

        // UseSetting, e não ConfigureAppConfiguration: o AddInfrastructure lê a connection string na hora do registro, dentro do Program,
        // antes de o ConfigureAppConfiguration da fábrica ser aplicado. Só o UseSetting (configuração do host) chega a tempo; com o
        // outro, a API usaria a connection string do appsettings.Development.json, ou seja, o banco de desenvolvimento.
        foreach (var (key, value) in values)
        {
            if (value is not null)
            {
                builder.UseSetting(key, value);
            }
        }

        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(Support.TestEndpointsController).Assembly));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
