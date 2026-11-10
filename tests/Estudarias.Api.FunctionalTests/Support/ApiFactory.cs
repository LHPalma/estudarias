using Estudarias.Api.Security;
using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Api.FunctionalTests.Support;

/// <summary>
/// A API inteira, com a composição e o pipeline reais, mais os endpoints de teste. Só a chave do JWT é trocada (por uma chave dos
/// testes), para o resultado não depender da chave de desenvolvimento. Nada aqui toca o banco: a política de autenticação se prova
/// sem ele.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Com 64 caracteres (512 bits) para o teste de outro algoritmo conseguir assinar em HS512 com exatamente esta chave: assim o token
    // só é recusado pela regra de algoritmo, e não por uma assinatura diferente.
    public const string SigningKey = "functional-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    public const string OtherSigningKey = "another-functional-test-key-9876543210-zyxwvutsrqponmlkjihgfedcba";

    public static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey }));

        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(TestEndpointsController).Assembly));
    }

    public static User Student(string email = "ana@example.com") =>
        User.RegisterStudent(
            email,
            PasswordHash.Create("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA", 1),
            Guid.CreateVersion7());

    /// <summary>O token que a API emitiria agora, pelo emissor real registrado.</summary>
    public string IssueToken(User user)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>().Issue(user).Token;
    }

    /// <summary>Um token emitido num instante qualquer (o passado, para um token vencido), com opções que o teste escolhe.</summary>
    public static string IssueTokenAt(User user, DateTimeOffset at, Action<JwtOptionsBuilder>? tweak = null)
    {
        var builder = new JwtOptionsBuilder();
        tweak?.Invoke(builder);
        var issuer = new JwtAccessTokenIssuer(Options.Create(builder.Build()), new FakeTimeProvider(at));
        return issuer.Issue(user).Token;
    }
}

public sealed class JwtOptionsBuilder
{
    public string SigningKey { get; set; } = ApiFactory.SigningKey;

    public string Issuer { get; set; } = "Estudarias";

    public string Audience { get; set; } = "Estudarias";

    internal JwtOptions Build() => new() { SigningKey = SigningKey, Issuer = Issuer, Audience = Audience };
}
