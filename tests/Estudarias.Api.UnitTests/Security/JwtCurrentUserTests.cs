using System.Security.Claims;
using Estudarias.Api.Security;
using Microsoft.AspNetCore.Http;

namespace Estudarias.Api.UnitTests.Security;

public class JwtCurrentUserTests
{
    private static JwtCurrentUser UserFor(ClaimsPrincipal? principal)
    {
        var accessor = new HttpContextAccessor();
        if (principal is not null)
        {
            accessor.HttpContext = new DefaultHttpContext { User = principal };
        }

        return new JwtCurrentUser(accessor);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    [Fact(DisplayName = "ICurrentUser devolve o id da claim sub da identidade autenticada")]
    public void UserId_ReadsTheSubClaim()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        var userId = UserFor(Authenticated(new Claim("sub", id.ToString()))).UserId;

        // Assert
        Assert.Equal(id, userId);
    }

    [Fact(DisplayName = "Sem requisição, ICurrentUser devolve nulo")]
    public void UserId_WithoutARequest_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(UserFor(null).UserId);
    }

    [Fact(DisplayName = "Uma identidade não autenticada devolve nulo, mesmo com a claim sub")]
    public void UserId_WithAnUnauthenticatedIdentity_ReturnsNull()
    {
        // Arrange
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.CreateVersion7().ToString())]));

        // Act & Assert
        Assert.Null(UserFor(anonymous).UserId);
    }

    [Theory(DisplayName = "Uma claim sub que não é um Guid, ou ausente, devolve nulo")]
    [InlineData("nao-e-guid")]
    [InlineData("")]
    public void UserId_WithABadSub_ReturnsNull(string sub)
    {
        // Act & Assert
        Assert.Null(UserFor(Authenticated(new Claim("sub", sub))).UserId);
    }

    [Fact(DisplayName = "Sem a claim sub, devolve nulo")]
    public void UserId_WithoutTheSubClaim_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(UserFor(Authenticated(new Claim("email", "ana@example.com"))).UserId);
    }
}
