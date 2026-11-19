using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Exercises;

public sealed class Exercise : BaseEntity, IAggregateRoot
{
    public const int MinAlternatives = 2;

    private readonly List<Alternative> _alternatives;

    public Guid LessonId { get; }
    public string Statement { get; }
    public Difficulty Difficulty { get; }
    public IReadOnlyCollection<Alternative> Alternatives => _alternatives;

    // Para o EF Core materializar: ele só preenche pelo construtor as propriedades simples, e as alternativas chegam depois, na
    // coleção. O Create continua sendo a única porta de entrada com validação.
    private Exercise(Guid lessonId, string statement, Difficulty difficulty)
        : this(lessonId, statement, difficulty, [])
    {
    }

    private Exercise(Guid lessonId, string statement, Difficulty difficulty, List<Alternative> alternatives)
    {
        LessonId = lessonId;
        Statement = statement;
        Difficulty = difficulty;
        _alternatives = alternatives;
    }

    public static Exercise Create(
        Guid lessonId, string statement, Difficulty difficulty, IReadOnlyCollection<Alternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);

        if (lessonId == Guid.Empty)
        {
            throw new DomainValidationException(ExerciseErrors.LessonRequired, "Exercise lesson is required.");
        }

        if (string.IsNullOrWhiteSpace(statement))
        {
            throw new DomainValidationException(ExerciseErrors.StatementRequired, "Exercise statement is required.");
        }

        if (!Enum.IsDefined(difficulty))
        {
            throw new DomainValidationException(ExerciseErrors.DifficultyInvalid, "Exercise difficulty is not valid.");
        }

        var copy = alternatives.ToList();
        EnsureValidAlternatives(copy);

        return new Exercise(lessonId, statement, difficulty, copy);
    }

    private static void EnsureValidAlternatives(List<Alternative> alternatives)
    {
        if (alternatives.Count < MinAlternatives)
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativesTooFew,
                $"An exercise must have at least {MinAlternatives} alternatives.");
        }

        var correct = alternatives.Count(alternative => alternative.IsCorrect);
        if (correct == 0)
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativesNoCorrect, "An exercise must have one correct alternative.");
        }

        if (correct > 1)
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativesMultipleCorrect, "An exercise must have only one correct alternative.");
        }

        var distinctTexts = alternatives
            .Select(alternative => alternative.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (distinctTexts != alternatives.Count)
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativeTextDuplicated, "The alternatives of an exercise must have distinct texts.");
        }
    }
}
