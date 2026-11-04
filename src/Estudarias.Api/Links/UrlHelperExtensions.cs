using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Links;

public static class UrlHelperExtensions
{
    /// <summary>Monta um <see cref="Link"/> com a URL absoluta da rota nomeada.</summary>
    public static Link LinkTo(this IUrlHelper url, string rel, string method, string routeName, object? values) =>
        new(
            url.Link(routeName, values)
                ?? throw new InvalidOperationException($"No route named '{routeName}' matches the given values."),
            rel,
            method);
}
