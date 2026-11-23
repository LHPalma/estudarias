using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Exercises;

public sealed class Alternative : BaseEntity
{
    public string Text { get; private set; }
    public bool IsCorrect { get; private set; }
    public Guid? ErrorTypeId { get; private set; }

    /// <summary>
    /// O lugar da alternativa no exercício, a partir de 1: é a ordem em que o conteúdo as escreveu. O id não serve de ordem (um
    /// <c>Guid</c> v7 não cresce dentro de uma mesma rajada de criação), então a ordem é gravada. Quem numera é o
    /// <see cref="Exercise"/>, pela ordem da lista que recebe; antes de entrar num exercício, a alternativa não tem posição (0).
    /// </summary>
    public int Position { get; private set; }

    private Alternative(string text, bool isCorrect, Guid? errorTypeId)
    {
        Text = text;
        IsCorrect = isCorrect;
        ErrorTypeId = errorTypeId;
    }

    public static Alternative Correct(string text) => new(ValidText(text), isCorrect: true, errorTypeId: null);

    public static Alternative Wrong(string text, Guid errorTypeId)
    {
        var validText = ValidText(text);

        if (errorTypeId == Guid.Empty)
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativeErrorTypeRequired, "A wrong alternative must have an error type.");
        }

        return new Alternative(validText, isCorrect: false, errorTypeId);
    }

    /// <summary>
    /// Assume o texto, a correção, o tipo de erro e a posição de <paramref name="revised"/>, mantendo o próprio id: é como a revisão
    /// do exercício preserva a alternativa para que as tentativas continuem apontando para ela.
    /// </summary>
    internal void TakeFrom(Alternative revised)
    {
        Text = revised.Text;
        IsCorrect = revised.IsCorrect;
        ErrorTypeId = revised.ErrorTypeId;
        Position = revised.Position;
    }

    internal void Place(int position) => Position = position;

    private static string ValidText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativeTextRequired, "Alternative text is required.");
        }

        return text.Trim();
    }
}
