namespace Estudarias.Api.Links;

/// <summary>
/// Os valores de <see cref="Link.Rel"/>. São parte do contrato com o app (tabela de links do <c>docs/api.md</c>): o app procura o
/// link pelo <c>rel</c>, então um valor publicado não muda de nome. Cada relação entra aqui junto com o primeiro endpoint que a usa.
/// </summary>
public static class LinkRelations
{
    /// <summary>O próprio recurso. Todo recurso tem, sempre.</summary>
    public const string Self = "self";

    /// <summary>Um tópico (<c>GET /topics/{id}</c>): o item da trilha e a lição apontam para ele.</summary>
    public const string Topic = "topic";

    /// <summary>O histórico de respostas do tópico (<c>GET /topics/{id}/attempts</c>): só existe quando o tópico não está bloqueado.</summary>
    public const string Attempts = "attempts";

    /// <summary>Responder um exercício (<c>POST /exercises/{id}/attempts</c>): só existe quando o tópico do exercício não está bloqueado.</summary>
    public const string Answer = "answer";

    /// <summary>As revisões do dia (<c>GET /reviews/due</c>): a trilha, a resposta e a revisão apontam para elas.</summary>
    public const string Reviews = "reviews";

    /// <summary>Revisar um cartão (<c>POST /reviews/{id}</c>): só existe nos cartões vencidos.</summary>
    public const string Review = "review";

    /// <summary>Uma lição (<c>GET /lessons/{id}</c>): só existe quando o tópico dela não está bloqueado.</summary>
    public const string Lesson = "lesson";

    /// <summary>A trilha do aluno (<c>GET /trail</c>), a tela inicial do app depois do login.</summary>
    public const string Trail = "trail";
}
