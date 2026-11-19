namespace Estudarias.Api.Links;

/// <summary>
/// Os valores de <see cref="Link.Rel"/>. São parte do contrato com o app (tabela de links do <c>docs/api.md</c>): o app procura o
/// link pelo <c>rel</c>, então um valor publicado não muda de nome. Cada relação entra aqui junto com o primeiro endpoint que a usa.
/// </summary>
public static class LinkRelations
{
    /// <summary>O próprio recurso. Todo recurso tem, sempre.</summary>
    public const string Self = "self";
}
