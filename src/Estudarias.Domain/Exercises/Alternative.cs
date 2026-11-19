using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Exercises;

public sealed class Alternative : BaseEntity
{
    public string Text { get; private set; }
    public bool IsCorrect { get; private set; }
    public Guid? ErrorTypeId { get; private set; }

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
    /// Assume o texto, a correção e o tipo de erro de <paramref name="revised"/>, mantendo o próprio id: é como a revisão do
    /// exercício preserva a alternativa para que as tentativas continuem apontando para ela.
    /// </summary>
    internal void TakeFrom(Alternative revised)
    {
        Text = revised.Text;
        IsCorrect = revised.IsCorrect;
        ErrorTypeId = revised.ErrorTypeId;
    }

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
