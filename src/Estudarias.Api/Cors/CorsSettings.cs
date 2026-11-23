namespace Estudarias.Api.Cors;

/// <summary>
/// As origens que podem chamar a API de um navegador (o app Expo aberto na web). O app nativo não usa CORS: só o navegador o exige.
/// Valem fora de Development, onde qualquer origem é aceita; vazio, que é o padrão, nenhuma origem é liberada.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Cada origem no formato que o navegador manda no cabeçalho <c>Origin</c>: esquema, host e porta, sem caminho nem barra no
    /// fim (<c>http://localhost:8081</c>). Vêm da configuração do ambiente (<c>Cors__AllowedOrigins__0</c>).
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Uma origem válida é http ou https, com host, e sem caminho, consulta nem barra final: o navegador compara o texto do
    /// <c>Origin</c> com a lista, então uma entrada com barra a mais nunca bateria, em silêncio. O curinga (<c>*</c>) não é origem:
    /// abrir a API para qualquer site tem de ser uma decisão visível, e não um valor esquecido na configuração.
    /// </summary>
    public static bool IsValidOrigin(string? origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && !string.IsNullOrEmpty(uri.Host)
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);
}
