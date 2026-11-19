using Estudarias.Application.Content;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Estudarias.Infrastructure.Content;

/// <summary>
/// Lê os arquivos <c>*.yaml</c> de uma pasta para os documentos da Application. Um arquivo que não é YAML válido, ou que tem uma
/// chave desconhecida (um erro de digitação, por exemplo), vira um problema com o arquivo e a linha, e não um item ignorado.
/// </summary>
internal static class YamlContentReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static IReadOnlyList<ContentFile> Read(string folder, List<ContentProblem> problems)
    {
        if (!Directory.Exists(folder))
        {
            problems.Add(new ContentProblem(folder, "pasta", "a pasta do conteúdo não existe."));
            return [];
        }

        var files = new List<ContentFile>();
        foreach (var path in Directory.GetFiles(folder, "*" + ContentValidator.FileExtension).Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var topic = Deserializer.Deserialize<TopicYaml?>(File.ReadAllText(path));
                if (topic is null)
                {
                    problems.Add(new ContentProblem(fileName, "arquivo", "o arquivo está vazio."));
                    continue;
                }

                files.Add(new ContentFile(fileName, topic.ToDocument()));
            }
            catch (YamlException exception)
            {
                var reason = exception.InnerException?.Message ?? exception.Message;
                problems.Add(new ContentProblem(fileName, $"linha {exception.Start.Line}", $"YAML inválido: {reason}"));
            }
        }

        if (files.Count == 0 && problems.Count == 0)
        {
            problems.Add(new ContentProblem(folder, "pasta", $"nenhum arquivo {ContentValidator.FileExtension} na pasta."));
        }

        return files;
    }

    // As classes abaixo são o formato do arquivo. Campos ausentes chegam nulos, e o validador os recusa pelas regras do Domain, com a
    // mesma mensagem de um valor inválido.

    private sealed class TopicYaml
    {
        public string? Slug { get; set; }
        public string? Name { get; set; }
        public List<string>? Prerequisites { get; set; }
        public List<ErrorTypeYaml>? ErrorTypes { get; set; }
        public List<LessonYaml>? Lessons { get; set; }
        public List<FlashcardYaml>? Flashcards { get; set; }

        public TopicDocument ToDocument() =>
            new(
                Slug!,
                Name!,
                Prerequisites ?? [],
                [.. (ErrorTypes ?? []).Select(errorType => errorType.ToDocument())],
                [.. (Lessons ?? []).Select(lesson => lesson.ToDocument())],
                [.. (Flashcards ?? []).Select(flashcard => flashcard.ToDocument())]);
    }

    private sealed class ErrorTypeYaml
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Feedback { get; set; }

        public ErrorTypeDocument ToDocument() => new(Code!, Name!, Description, Feedback!);
    }

    private sealed class LessonYaml
    {
        public int Order { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public List<ExerciseYaml>? Exercises { get; set; }

        public LessonDocument ToDocument() =>
            new(Order, Title!, Content!, [.. (Exercises ?? []).Select(exercise => exercise.ToDocument())]);
    }

    private sealed class ExerciseYaml
    {
        public string? Code { get; set; }
        public string? Statement { get; set; }
        public string? Difficulty { get; set; }
        public List<AlternativeYaml>? Alternatives { get; set; }

        public ExerciseDocument ToDocument() =>
            new(Code!, Statement!, Difficulty!, [.. (Alternatives ?? []).Select(alternative => alternative.ToDocument())]);
    }

    private sealed class AlternativeYaml
    {
        public string? Text { get; set; }
        public bool Correct { get; set; }
        public string? ErrorType { get; set; }

        public AlternativeDocument ToDocument() => new(Text!, Correct, ErrorType);
    }

    private sealed class FlashcardYaml
    {
        public string? Code { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? ErrorType { get; set; }

        public FlashcardDocument ToDocument() => new(Code!, Question!, Answer!, ErrorType);
    }
}
