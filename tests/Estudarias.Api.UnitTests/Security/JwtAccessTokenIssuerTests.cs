using System.Text;
using Estudarias.Api.Security;
using Estudarias.Api.UnitTests.Support;
using Estudarias.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Estudarias.Api.UnitTests.Security;

public class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private static readonly User Ana = User.RegisterStudent(
        "ana@example.com",
        PasswordHash.Create("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA", 1),
        Guid.CreateVersion7());

    private static JwtAccessTokenIssuer IssuerWith(JwtOptions? options = null, TimeProvider? clock = null) =>
        new(
            Options.Create(options ?? new JwtOptions { SigningKey = TestSecrets.SigningKey }),
            clock ?? new FakeTimeProvider(Start));

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact(DisplayName = "RF-04: o token carrega o id da conta, o e-mail e o papel, e mais nada de dado da conta")]
    public void Issue_CarriesTheAccountIdEmailAndRole()
    {
        // Act
        var token = Read(IssuerWith().Issue(Ana).Token);

        // Assert
        Assert.Equal(Ana.Id.ToString(), token.GetClaim("sub").Value);
        Assert.Equal("ana@example.com", token.GetClaim("email").Value);
        Assert.Equal("Student", token.GetClaim("role").Value);
        Assert.Equal(
            ["aud", "email", "exp", "iat", "iss", "nbf", "role", "sub"],
            token.Claims.Select(claim => claim.Type).Order().ToArray());
    }

    [Fact(DisplayName = "RF-04: o token nunca carrega a senha, o hash nem o pepper")]
    public void Issue_NeverCarriesThePasswordHash()
    {
        // Act
        var raw = IssuerWith().Issue(Ana).Token;
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(raw.Split('.')[1]));

        // Assert
        Assert.DoesNotContain("argon2", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "RF-04: o token vale 15 minutos a partir da emissão, e a expiração devolvida é a do token")]
    public void Issue_ExpiresFifteenMinutesAfterIssuance()
    {
        // Act
        var issued = IssuerWith().Issue(Ana);
        var token = Read(issued.Token);

        // Assert
        Assert.Equal(Start.AddMinutes(15), issued.ExpiresAt);
        Assert.Equal(Start.AddMinutes(15).UtcDateTime, token.ValidTo);
        Assert.Equal(Start.UtcDateTime, token.ValidFrom);
        Assert.Equal(Start.UtcDateTime, token.IssuedAt);
    }

    [Fact(DisplayName = "RF-04: a vida do token vem da configuração")]
    public void Issue_UsesTheConfiguredLifetime()
    {
        // Arrange
        var issuer = IssuerWith(new JwtOptions { SigningKey = TestSecrets.SigningKey, AccessTokenMinutes = 5 });

        // Act
        var issued = issuer.Issue(Ana);

        // Assert
        Assert.Equal(Start.AddMinutes(5), issued.ExpiresAt);
    }

    [Fact(DisplayName = "RF-04: o tempo é o do relógio injetado, não o relógio do sistema")]
    public void Issue_UsesTheInjectedClock()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var issuer = IssuerWith(clock: clock);

        // Act
        var first = issuer.Issue(Ana);
        clock.Advance(TimeSpan.FromHours(2));
        var second = issuer.Issue(Ana);

        // Assert
        Assert.Equal(TimeSpan.FromHours(2), second.ExpiresAt - first.ExpiresAt);
    }

    [Fact(DisplayName = "RF-04: o emissor e a audiência são os configurados")]
    public void Issue_UsesTheConfiguredIssuerAndAudience()
    {
        // Arrange
        var issuer = IssuerWith(new JwtOptions { SigningKey = TestSecrets.SigningKey, Issuer = "Quem", Audience = "Para quem" });

        // Act
        var token = Read(issuer.Issue(Ana).Token);

        // Assert
        Assert.Equal("Quem", token.Issuer);
        Assert.Contains("Para quem", token.Audiences);
    }

    [Fact(DisplayName = "RF-04: o token é assinado em HS256 e só confere com a mesma chave")]
    public async Task Issue_IsSignedWithHs256AndOnlyValidatesWithTheSameKey()
    {
        // Arrange
        var raw = IssuerWith().Issue(Ana).Token;
        var handler = new JsonWebTokenHandler();

        TokenValidationParameters Parameters(string key) => new()
        {
            ValidIssuer = "Estudarias",
            ValidAudience = "Estudarias",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false,
        };

        // Act
        var withTheKey = await handler.ValidateTokenAsync(raw, Parameters(TestSecrets.SigningKey));
        var withAnotherKey = await handler.ValidateTokenAsync(raw, Parameters(TestSecrets.OtherSigningKey));

        // Assert
        Assert.Equal("HS256", Read(raw).Alg);
        Assert.True(withTheKey.IsValid);
        Assert.False(withAnotherKey.IsValid);
    }
}
