namespace Estudarias.Api.Links;

/// <summary>Ação disponível a partir de um recurso. Formato próprio; ver docs/adr/0001.</summary>
public sealed record Link(string Href, string Rel, string Method);
