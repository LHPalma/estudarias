using Estudarias.Domain.StudentFlashcards.Scheduling;

namespace Estudarias.Api.Contracts.Reviews;

/// <summary>
/// Como foi a revisão: <c>again</c> (errei), <c>hard</c>, <c>good</c> ou <c>easy</c>, em minúsculas. O app pode mostrar só dois botões
/// (<c>again</c> e <c>good</c>) ou os quatro. Uma nota ausente ou desconhecida falha com <c>studentFlashcard.rating.invalid</c>.
/// </summary>
public sealed record ReviewRequest(string? Rating)
{
    /// <summary>A nota do enum, ou nula se o texto não é uma das quatro (o caso de uso recusa a nula).</summary>
    public ReviewRating? ToRating() => Rating switch
    {
        "again" => ReviewRating.Again,
        "hard" => ReviewRating.Hard,
        "good" => ReviewRating.Good,
        "easy" => ReviewRating.Easy,
        _ => null,
    };
}
