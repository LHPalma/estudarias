using Estudarias.Api.Links;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Estudarias.Api.UnitTests.Links;

public class UrlHelperLinkExtensionsTests
{
    [Fact(DisplayName = "O link leva a URL que a rota nomeada gera, com o rel e o método pedidos")]
    public void LinkTo_WithAKnownRoute_BuildsTheLinkFromTheRoute()
    {
        // Arrange
        var url = new FakeUrlHelper("http://localhost/topics/42");

        // Act
        var link = url.LinkTo("self", HttpMethods.Get, "GetTopic", new { id = 42 });

        // Assert
        Assert.Equal(new Link("http://localhost/topics/42", "self", "GET"), link);
        Assert.Equal("GetTopic", url.RouteName);
        Assert.Equal(new { id = 42 }, url.RouteValues);
    }

    [Fact(DisplayName = "Uma rota que não gera URL é erro de programação, e não um link sem destino")]
    public void LinkTo_WhenTheRouteGeneratesNoUrl_Throws()
    {
        // Arrange
        var url = new FakeUrlHelper(generatedUrl: null);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => url.LinkTo("self", HttpMethods.Get, "NoSuchRoute"));
        Assert.Contains("NoSuchRoute", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Só o <c>Link</c> é usado: ele devolve a URL combinada e guarda o que recebeu.</summary>
    private sealed class FakeUrlHelper(string? generatedUrl) : IUrlHelper
    {
        public string? RouteName { get; private set; }

        public object? RouteValues { get; private set; }

        public ActionContext ActionContext => throw new NotSupportedException();

        public string? Link(string? routeName, object? values)
        {
            RouteName = routeName;
            RouteValues = values;
            return generatedUrl;
        }

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => throw new NotSupportedException();

        public bool IsLocalUrl(string? url) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext) => throw new NotSupportedException();
    }
}
