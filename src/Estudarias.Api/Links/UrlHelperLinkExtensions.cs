using Microsoft.AspNetCore.Mvc;

namespace Estudarias.Api.Links;

public static class UrlHelperLinkExtensions
{
    /// <summary>
    /// Monta o link para a rota chamada <paramref name="routeName"/>, preenchida com <paramref name="routeValues"/>. A URL sai da
    /// própria rota (<c>Url.Link</c>), absoluta, com o esquema e o host da requisição: o template existe num lugar só, o atributo
    /// da action, e o link acompanha qualquer mudança nele.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A rota não gera URL: o nome não existe ou falta um valor que o template pede. <c>Url.Link</c> devolveria <c>null</c> em
    /// silêncio, e o app receberia um link sem destino; aqui o erro de programação aparece no primeiro teste que passa por ele.
    /// </exception>
    public static Link LinkTo(this IUrlHelper url, string rel, string method, string routeName, object? routeValues = null)
    {
        var href = url.Link(routeName, routeValues)
            ?? throw new InvalidOperationException(
                $"A rota '{routeName}' não gerou URL para o link '{rel}': o nome não existe ou falta um valor do template.");

        return new Link(href, rel, method);
    }
}
