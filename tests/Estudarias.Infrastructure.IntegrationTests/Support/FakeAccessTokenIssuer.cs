using Estudarias.Application.Identity;
using Estudarias.Domain.Identity;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

/// <summary>
/// No lugar do emissor de JWT, que é da API (#40): os testes de fluxo provam a Application com a Infrastructure de verdade, e o
/// access token só precisa existir e ter validade.
/// </summary>
internal sealed class FakeAccessTokenIssuer(TimeProvider clock) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(User user) =>
        new($"access-token-for-{user.Id}", clock.GetUtcNow().AddMinutes(15));
}
