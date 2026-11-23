using Estudarias.Domain.Flashcards;

namespace Estudarias.Application.Flashcards;

/// <summary>O flashcard de conteúdo (o que o tipo de erro gera). Só a carga o escreve, então aqui não há <c>Add</c>.</summary>
public interface IFlashcardRepository
{
    /// <summary>
    /// O flashcard gerado por aquele tipo de erro, sem rastreamento, ou nulo quando o conteúdo não tem um (PEND-RSP-02). Há no máximo
    /// um por tipo de erro: o banco o garante por um índice único parcial.
    /// </summary>
    Task<Flashcard?> FindGeneratedByAsync(Guid errorTypeId, CancellationToken cancellationToken);
}
