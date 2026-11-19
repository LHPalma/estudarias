namespace Estudarias.Api.Links;

/// <summary>
/// Uma ação que o cliente pode fazer a partir de um recurso: para onde (<c>Href</c>, URL absoluta), o que ela significa
/// (<c>Rel</c>, um dos valores de <see cref="LinkRelations"/>) e com qual método HTTP (<c>Method</c>). O app mostra um botão só
/// quando o link correspondente existe, sem reimplementar a regra que decide se a ação é permitida (ADR-004).
/// </summary>
public sealed record Link(string Href, string Rel, string Method);
