using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Topics;

namespace Estudarias.Application.Content;

/// <summary>
/// Confere o conteúdo inteiro antes de gravar qualquer coisa (RF-CNT-07): as regras de cada agregado, o grafo de pré-requisitos, os
/// tipos de erro de cada tópico e as chaves naturais. Devolve todos os problemas, com o arquivo e o item; lista vazia é conteúdo
/// válido. É lógica pura: o que já está no banco chega por <c>storedPrerequisites</c>.
/// </summary>
public static class ContentValidator
{
    /// <summary>A extensão dos arquivos de conteúdo; o nome do arquivo sem ela é o slug do tópico.</summary>
    public const string FileExtension = ".yaml";

    private static readonly Dictionary<string, Difficulty> Difficulties = new(StringComparer.Ordinal)
    {
        ["easy"] = Difficulty.Easy,
        ["medium"] = Difficulty.Medium,
        ["hard"] = Difficulty.Hard,
    };

    /// <summary>A dificuldade do YAML (<c>easy</c>, <c>medium</c> ou <c>hard</c>), ou nulo quando não é nenhuma delas.</summary>
    public static Difficulty? ParseDifficulty(string? value) =>
        value is not null && Difficulties.TryGetValue(value, out var difficulty) ? difficulty : null;

    /// <param name="files">Os arquivos da carga.</param>
    /// <param name="storedPrerequisites">
    /// Os tópicos que já estão no banco, pelo slug, com os slugs dos pré-requisitos de cada um. Servem para aceitar um pré-requisito
    /// que só existe no banco e para achar um ciclo que passa por um tópico que não está na carga.
    /// </param>
    public static IReadOnlyList<ContentProblem> Validate(
        IReadOnlyList<ContentFile> files, IReadOnlyDictionary<string, IReadOnlyCollection<string>> storedPrerequisites)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(storedPrerequisites);

        var problems = new List<ContentProblem>();

        foreach (var file in files)
        {
            ValidateFile(file, files, problems);
        }

        ValidateSlugsAcrossFiles(files, problems);
        ValidatePrerequisites(files, storedPrerequisites, problems);

        return problems;
    }

    private static void ValidateFile(ContentFile file, IReadOnlyList<ContentFile> files, List<ContentProblem> problems)
    {
        var topic = file.Topic;
        void Problem(string item, string message) => problems.Add(new ContentProblem(file.FileName, item, message));

        Guid topicId;
        try
        {
            topicId = Topic.Create(topic.Name, topic.Slug).Id;
        }
        catch (DomainValidationException exception)
        {
            Problem("tópico", Describe(exception));
            topicId = Guid.CreateVersion7();
        }

        var expectedFile = topic.Slug + FileExtension;
        if (!string.Equals(Path.GetFileName(file.FileName), expectedFile, StringComparison.Ordinal))
        {
            Problem("tópico", $"o arquivo de um tópico se chama <slug>{FileExtension}: este tópico deveria estar em '{expectedFile}'.");
        }

        // Os tipos de erro do tópico: cada um com id falso só para os agregados que apontam para ele poderem ser montados.
        var errorTypeIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var errorType in topic.ErrorTypes)
        {
            var item = $"tipo de erro '{errorType.Code}'";
            if (errorTypeIds.ContainsKey(errorType.Code ?? ""))
            {
                Problem(item, "código repetido no tópico.");
                continue;
            }

            try
            {
                errorTypeIds[errorType.Code!] =
                    ErrorType.Create(topicId, errorType.Code!, errorType.Name, errorType.Description, errorType.Feedback).Id;
            }
            catch (DomainValidationException exception)
            {
                Problem(item, Describe(exception));
            }
        }

        Guid? ErrorTypeOf(string? code, string item)
        {
            if (code is null)
            {
                return null;
            }

            if (errorTypeIds.TryGetValue(code, out var id))
            {
                return id;
            }

            var owner = files.FirstOrDefault(other =>
                other != file && other.Topic.ErrorTypes.Any(errorType => errorType.Code == code));
            Problem(
                item,
                owner is null
                    ? $"o tipo de erro '{code}' não está declarado no tópico."
                    : $"o tipo de erro '{code}' é do tópico '{owner.Topic.Slug}', e não deste (RN-CNT-09).");

            // Um id qualquer: o problema já foi registrado, e o item continua sendo conferido pelas outras regras sem repeti-lo.
            return Guid.CreateVersion7();
        }

        var lessonOrders = new HashSet<int>();
        var exerciseCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lesson in topic.Lessons)
        {
            var lessonItem = $"lição {lesson.Order}";
            if (!lessonOrders.Add(lesson.Order))
            {
                Problem(lessonItem, "ordem repetida no tópico.");
            }

            Guid lessonId;
            try
            {
                lessonId = Lesson.Create(topicId, lesson.Order, lesson.Title, lesson.Content).Id;
            }
            catch (DomainValidationException exception)
            {
                Problem(lessonItem, Describe(exception));
                lessonId = Guid.CreateVersion7();
            }

            foreach (var exercise in lesson.Exercises)
            {
                ValidateExercise(exercise, lessonId, exerciseCodes, ErrorTypeOf, Problem);
            }
        }

        var flashcardCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var flashcard in topic.Flashcards)
        {
            var item = $"flashcard '{flashcard.Code}'";
            if (!flashcardCodes.Add(flashcard.Code ?? ""))
            {
                Problem(item, "código repetido no tópico.");
                continue;
            }

            var errorTypeId = ErrorTypeOf(flashcard.ErrorType, item);
            try
            {
                _ = errorTypeId is { } id
                    ? Flashcard.CreateFromError(topicId, flashcard.Code!, id, flashcard.Question, flashcard.Answer)
                    : Flashcard.CreateManual(topicId, flashcard.Code!, flashcard.Question, flashcard.Answer);
            }
            catch (DomainValidationException exception)
            {
                Problem(item, Describe(exception));
            }
        }
    }

    private static void ValidateExercise(
        ExerciseDocument exercise,
        Guid lessonId,
        HashSet<string> exerciseCodes,
        Func<string?, string, Guid?> errorTypeOf,
        Action<string, string> problem)
    {
        var item = $"exercício '{exercise.Code}'";
        if (!exerciseCodes.Add(exercise.Code ?? ""))
        {
            problem(item, "código repetido no tópico.");
            return;
        }

        var difficulty = ParseDifficulty(exercise.Difficulty);
        if (difficulty is null)
        {
            problem(item, $"a dificuldade '{exercise.Difficulty}' não existe: use easy, medium ou hard.");
        }

        var alternatives = new List<Alternative>();
        foreach (var alternative in exercise.Alternatives)
        {
            var alternativeItem = $"alternativa '{alternative.Text}' do exercício '{exercise.Code}'";
            try
            {
                if (alternative.Correct)
                {
                    if (alternative.ErrorType is not null)
                    {
                        problem(alternativeItem, "a alternativa correta não tem tipo de erro (RN-CNT-06).");
                    }

                    alternatives.Add(Alternative.Correct(alternative.Text));
                }
                else
                {
                    alternatives.Add(Alternative.Wrong(alternative.Text, errorTypeOf(alternative.ErrorType, alternativeItem) ?? Guid.Empty));
                }
            }
            catch (DomainValidationException exception)
            {
                problem(alternativeItem, Describe(exception));
            }
        }

        // As regras do conjunto (no mínimo 2, exatamente 1 correta, textos distintos) só se conferem com todas as alternativas montadas.
        if (difficulty is not null && alternatives.Count == exercise.Alternatives.Count)
        {
            try
            {
                Exercise.Create(lessonId, exercise.Code!, exercise.Statement, difficulty.Value, alternatives);
            }
            catch (DomainValidationException exception)
            {
                problem(item, Describe(exception));
            }
        }
    }

    private static void ValidateSlugsAcrossFiles(IReadOnlyList<ContentFile> files, List<ContentProblem> problems)
    {
        foreach (var group in files.GroupBy(file => file.Topic.Slug, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            var names = string.Join(", ", group.Select(file => file.FileName));
            foreach (var file in group)
            {
                problems.Add(new ContentProblem(file.FileName, "tópico", $"o slug '{group.Key}' aparece em mais de um arquivo: {names}."));
            }
        }
    }

    private static void ValidatePrerequisites(
        IReadOnlyList<ContentFile> files,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> storedPrerequisites,
        List<ContentProblem> problems)
    {
        // O grafo da carga: os tópicos dos arquivos com os pré-requisitos do YAML, e os que só estão no banco com os do banco.
        var graph = storedPrerequisites.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal);
        foreach (var file in files)
        {
            graph[file.Topic.Slug] = [.. file.Topic.Prerequisites];
        }

        foreach (var file in files)
        {
            var topic = file.Topic;
            foreach (var prerequisite in topic.Prerequisites)
            {
                var item = $"pré-requisito '{prerequisite}'";
                if (prerequisite == topic.Slug)
                {
                    problems.Add(new ContentProblem(file.FileName, item, "um tópico não é pré-requisito de si mesmo."));
                }
                else if (!graph.ContainsKey(prerequisite))
                {
                    problems.Add(new ContentProblem(file.FileName, item, "o tópico não existe nos arquivos nem no banco (RN-CNT-08)."));
                }
            }

            foreach (var repeated in topic.Prerequisites.GroupBy(slug => slug).Where(group => group.Count() > 1))
            {
                problems.Add(new ContentProblem(file.FileName, $"pré-requisito '{repeated.Key}'", "repetido no tópico."));
            }
        }

        foreach (var file in files)
        {
            if (FindCycle(file.Topic.Slug, graph) is { } cycle)
            {
                problems.Add(new ContentProblem(
                    file.FileName, "pré-requisitos", $"ciclo no grafo de pré-requisitos: {string.Join(" → ", cycle)} (RN-CNT-08)."));
            }
        }
    }

    /// <summary>O caminho de um ciclo que volta a <paramref name="start"/>, ou nulo. Cada arquivo do ciclo recebe o seu problema.</summary>
    private static List<string>? FindCycle(string start, Dictionary<string, List<string>> graph)
    {
        var path = new List<string> { start };
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string slug)
        {
            foreach (var next in graph.GetValueOrDefault(slug) ?? [])
            {
                // O tópico que exige a si mesmo já tem o seu problema; aqui só contam os ciclos entre tópicos diferentes.
                if (next == slug)
                {
                    continue;
                }

                if (next == start)
                {
                    path.Add(next);
                    return true;
                }

                if (!visited.Add(next))
                {
                    continue;
                }

                path.Add(next);
                if (Visit(next))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        return Visit(start) ? path : null;
    }

    private static string Describe(DomainValidationException exception) => $"{exception.Message} ({exception.Code})";
}
