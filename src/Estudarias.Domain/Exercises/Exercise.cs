using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Exercises;

public sealed class Exercise : BaseEntity, IAggregateRoot
{
    public const int MinAlternatives = 2;
    public const int CodeMaxLength = ContentCode.MaxLength;

    private readonly List<Alternative> _alternatives;

    public Guid LessonId { get; private set; }
    public string Code { get; }
    public string Statement { get; private set; }
    public Difficulty Difficulty { get; private set; }
    public IReadOnlyCollection<Alternative> Alternatives => _alternatives;

    // Para o EF Core materializar: ele só preenche pelo construtor as propriedades simples, e as alternativas chegam depois, na
    // coleção. O Create continua sendo a única porta de entrada com validação.
    private Exercise(Guid lessonId, string code, string statement, Difficulty difficulty)
        : this(lessonId, code, statement, difficulty, [])
    {
    }

    private Exercise(Guid lessonId, string code, string statement, Difficulty difficulty, List<Alternative> alternatives)
    {
        LessonId = lessonId;
        Code = code;
        Statement = statement;
        Difficulty = difficulty;
        _alternatives = alternatives;
    }

    /// <param name="code">A chave natural do exercício na carga do conteúdo (formato de <see cref="ContentCode"/>).</param>
    public static Exercise Create(
        Guid lessonId, string code, string statement, Difficulty difficulty, IReadOnlyCollection<Alternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);

        EnsureValidLesson(lessonId);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainValidationException(ExerciseErrors.CodeRequired, "Exercise code is required.");
        }

        if (!ContentCode.IsValid(code))
        {
            throw new DomainValidationException(
                ExerciseErrors.CodeInvalid,
                $"Exercise code must start with a lowercase letter and have lowercase letters and digits separated by single underscores, with at most {CodeMaxLength} characters.");
        }

        EnsureValidContent(statement, difficulty);

        var copy = alternatives.ToList();
        EnsureValidAlternatives(copy);

        return new Exercise(lessonId, code, statement, difficulty, copy);
    }

    /// <summary>
    /// Revisa o exercício, com as regras da criação: a lição (o exercício pode mudar de lição dentro do tópico), o enunciado, a
    /// dificuldade e as alternativas. O código não muda: é a chave natural.
    /// </summary>
    /// <remarks>
    /// As alternativas são casadas pelo texto, sem diferenciar maiúsculas: a que continua mantém o id e assume a correção e o tipo de
    /// erro novos, e a nova entra com id novo. Remover uma alternativa é recusado (<c>exercise.alternative.removed</c>), porque
    /// tentativas já gravadas podem apontar para ela. Tudo é validado antes de qualquer mudança.
    /// </remarks>
    public void Revise(Guid lessonId, string statement, Difficulty difficulty, IReadOnlyCollection<Alternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);

        EnsureValidLesson(lessonId);
        EnsureValidContent(statement, difficulty);

        var revised = alternatives.ToList();
        EnsureValidAlternatives(revised);

        var removed = _alternatives
            .Where(current => !revised.Any(alternative => SameText(alternative, current)))
            .Select(current => current.Text)
            .ToList();
        if (removed.Count > 0)
        {
            throw new DomainValidationException(
                ExerciseErrors.AlternativeRemoved,
                $"Alternatives of an exercise cannot be removed, because attempts may point to them: {string.Join(", ", removed)}.");
        }

        LessonId = lessonId;
        Statement = statement;
        Difficulty = difficulty;

        foreach (var alternative in revised)
        {
            var current = _alternatives.FirstOrDefault(existing => SameText(existing, alternative));
            if (current is null)
            {
                _alternatives.Add(alternative);
            }
            else
            {
                current.TakeFrom(alternative);
            }
        }
    }

    private static bool SameText(Alternative first, Alternative second) =>
        string.Equals(first.Text, second.Text, StringComparison.OrdinalIgnoreCase);

    private static void EnsureValidLesson(Guid lessonId)
    {
        if (lessonId == Guid.Empty)
        {
            throw new DomainValidationException(ExerciseErrors.LessonRequired, "Exercise lesson is required.");
        }
    }

    private static void EnsureValidContent(string statement, Difficulty difficulty)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            throw new DomainValidationException(ExerciseErrors.StatementRequired, "Exercise statement is required.");
        }

        if (!Enum.IsDefined(difficulty))
        {
            throw new DomainValidationException(ExerciseErrors.DifficultyInvalid, "Exercise difficulty is not valid.");
        }
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
