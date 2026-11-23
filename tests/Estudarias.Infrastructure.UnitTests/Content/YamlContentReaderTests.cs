using Estudarias.Application.Content;
using Estudarias.Infrastructure.Content;

namespace Estudarias.Infrastructure.UnitTests.Content;

public sealed class YamlContentReaderTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("estudarias-content-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private void WriteFile(string name, string yaml) => File.WriteAllText(Path.Combine(_folder, name), yaml);

    /// <summary>A pasta content/ do repositório, achada subindo a partir da saída do teste até o Estudarias.slnx.</summary>
    private static string RepositoryContentFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Estudarias.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Estudarias.slnx not found."), "content");
    }

    [Fact(DisplayName = "O conteúdo do repositório é lido e passa na validação inteira")]
    public void Read_TheRepositoryContent_IsValid()
    {
        // Arrange
        var problems = new List<ContentProblem>();

        // Act
        var files = YamlContentReader.Read(RepositoryContentFolder(), problems);
        problems.AddRange(ContentValidator.Validate(files, new Dictionary<string, IReadOnlyCollection<string>>()));

        // Assert
        Assert.Empty(problems);
        Assert.Equal(["fracoes", "operacoes-basicas"], files.Select(file => file.Topic.Slug).Order());
    }

    [Fact(DisplayName = "O YAML vira os documentos, com a alternativa errada por padrão e o flashcard sem tipo de erro como manual")]
    public void Read_MapsTheYamlToDocuments()
    {
        // Arrange
        WriteFile("fracoes.yaml", """
            slug: fracoes
            name: Frações
            prerequisites: [operacoes-basicas]
            errorTypes:
              - code: soma_direta
                name: Soma direta
                feedback: Iguale os denominadores.
            lessons:
              - order: 1
                title: Soma
                content: Texto.
                exercises:
                  - code: meio_mais_terco
                    statement: Quanto é {1/2} + {1/3}?
                    difficulty: medium
                    alternatives:
                      - text: "{5/6}"
                        correct: true
                      - text: "{2/5}"
                        errorType: soma_direta
            flashcards:
              - code: manual
                question: Pergunta?
                answer: Resposta.
            """);
        var problems = new List<ContentProblem>();

        // Act
        var file = Assert.Single(YamlContentReader.Read(_folder, problems));

        // Assert
        Assert.Empty(problems);
        Assert.Equal(("fracoes.yaml", "fracoes", "Frações"), (file.FileName, file.Topic.Slug, file.Topic.Name));
        Assert.Equal(["operacoes-basicas"], file.Topic.Prerequisites);
        Assert.Equal(new ErrorTypeDocument("soma_direta", "Soma direta", null, "Iguale os denominadores."), Assert.Single(file.Topic.ErrorTypes));
        var exercise = Assert.Single(Assert.Single(file.Topic.Lessons).Exercises);
        Assert.Equal(("meio_mais_terco", "Quanto é {1/2} + {1/3}?", "medium"), (exercise.Code, exercise.Statement, exercise.Difficulty));
        Assert.Equal(
            [new AlternativeDocument("{5/6}", true, null), new AlternativeDocument("{2/5}", false, "soma_direta")],
            exercise.Alternatives);
        Assert.Null(Assert.Single(file.Topic.Flashcards).ErrorType);
    }

    [Fact(DisplayName = "Uma chave desconhecida no YAML é um problema com o arquivo e a linha, e não um campo ignorado")]
    public void Read_WithAnUnknownKey_ReportsTheLine()
    {
        // Arrange
        WriteFile("fracoes.yaml", "slug: fracoes\nname: Frações\nprerequisite: [operacoes-basicas]\n");
        var problems = new List<ContentProblem>();

        // Act
        var files = YamlContentReader.Read(_folder, problems);

        // Assert
        Assert.Empty(files);
        var problem = Assert.Single(problems);
        Assert.Equal(("fracoes.yaml", "linha 3"), (problem.FileName, problem.Item));
    }

    [Fact(DisplayName = "Uma pasta sem arquivo de conteúdo é um problema")]
    public void Read_WithAnEmptyFolder_ReportsIt()
    {
        // Arrange
        var problems = new List<ContentProblem>();

        // Act
        var files = YamlContentReader.Read(_folder, problems);

        // Assert
        Assert.Empty(files);
        Assert.Equal("pasta", Assert.Single(problems).Item);
    }
}
