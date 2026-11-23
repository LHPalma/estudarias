using Estudarias.Domain.Common;
using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Exercises;

public sealed class Exercise : BaseEntity, IAggregateRoot
{
    public const int MinAlternatives = 2;
    public const int CodeMaxLength = ContentCode.MaxLength;

    private readonly List<Alternative> _alternatives;

    public Guid LessonId { get; private set; }

    /// <summary>
    /// O lugar do exercício na lição, a partir de 1: a ordem em que o conteúdo o escreveu. É gravada porque o id não serve de ordem
    /// (um <c>Guid</c> v7 não cresce dentro de uma mesma rajada de criação).
    /// </summary>
    public int Position { get; private set; }

    public string Code { get; }
    public string Statement { get; private set; }
    public Difficulty Difficulty { get; private set; }

    /// <summary>As alternativas na ordem do conteúdo (por <see cref="Alternative.Position"/>), e não na ordem em que o banco as devolveu.</summary>
    public IReadOnlyCollection<Alternative> Alternatives => [.. _alternatives.OrderBy(alternative => alternative.Position)];

    // Para o EF Core materializar: ele só preenche pelo construtor as propriedades simples, e as alternativas chegam depois, na
    // coleção. O Create continua sendo a única porta de entrada com validação.
    private Exercise(Guid lessonId, int position, string code, string statement, Difficulty difficulty)
        : this(lessonId, position, code, statement, difficulty, [])
    {
    }

    private Exercise(
        Guid lessonId, int position, string code, string statement, Difficulty difficulty, List<Alternative> alternatives)
    {
        LessonId = lessonId;
        Position = position;
        Code = code;
        Statement = statement;
        Difficulty = difficulty;
        _alternatives = alternatives;
    }

    /// <param name="position">O lugar do exercício na lição, a partir de 1.</param>
    /// <param name="code">A chave natural do exercício na carga do conteúdo (formato de <see cref="ContentCode"/>).</param>
    /// <param name="alternatives">As alternativas, na ordem do conteúdo: elas são numeradas por essa ordem.</param>
    public static Exercise Create(
        Guid lessonId,
        int position,
        string code,
        string statement,
        Difficulty difficulty,
        IReadOnlyCollection<Alternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);

        EnsureValidLesson(lessonId);
        EnsureValidPosition(position);

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
        Number(copy);

        return new Exercise(lessonId, position, code, statement, difficulty, copy);
    }

    /// <summary>
    /// Revisa o exercício, com as regras da criação: a lição (o exercício pode mudar de lição dentro do tópico), o lugar nela, o
    /// enunciado, a dificuldade e as alternativas. O código não muda: é a chave natural.
    /// </summary>
    /// <remarks>
    /// As alternativas são casadas pelo texto, sem diferenciar maiúsculas: a que continua mantém o id e assume a correção e o tipo de
    /// erro novos, e a nova entra com id novo. Remover uma alternativa é recusado (<c>exercise.alternative.removed</c>), porque
    /// tentativas já gravadas podem apontar para ela. A ordem da lista revisada é a nova ordem: a alternativa que continua só muda de
    /// posição. Tudo é validado antes de qualquer mudança.
    /// </remarks>
    public void Revise(
        Guid lessonId, int position, string statement, Difficulty difficulty, IReadOnlyCollection<Alternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);

        EnsureValidLesson(lessonId);
        EnsureValidPosition(position);
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

        Number(revised);

        LessonId = lessonId;
        Position = position;
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

    private static void Number(List<Alternative> alternatives)
    {
        for (var index = 0; index < alternatives.Count; index++)
        {
            alternatives[index].Place(index + 1);
        }
    }

    private static void EnsureValidPosition(int position)
    {
        if (position < 1)
        {
            throw new DomainValidationException(ExerciseErrors.PositionInvalid, "Exercise position must start at 1.");
        }
    }

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
