using Estudarias.Application.Content;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.IntegrationTests.Content;

/// <summary>A carga do conteúdo (#85) contra um PostgreSQL real: carga inicial, recarga idempotente, revisão e tudo ou nada.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ContentLoaderTests(PostgresFixture postgres) : DatabaseTests(postgres), IDisposable
{
    private const string Basics = """
        slug: operacoes-basicas
        name: Operações básicas
        errorTypes:
          - code: esquece_o_vai_um
            name: Esquece o vai um
            feedback: A dezena vai para a coluna da esquerda.
        lessons:
          - order: 1
            title: Soma
            content: Texto da soma.
            exercises:
              - code: soma_com_vai_um
                statement: Quanto é 27 + 15?
                difficulty: easy
                alternatives:
                  - text: "42"
                    correct: true
                  - text: "32"
                    errorType: esquece_o_vai_um
        flashcards:
          - code: por_que_vai_um
            errorType: esquece_o_vai_um
            question: Por que 27 + 15 não é 32?
            answer: Porque a dezena vai um.
        """;

    private readonly List<string> _folders = [];

    public void Dispose()
    {
        foreach (var folder in _folders)
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string Folder(params (string Name, string Yaml)[] files)
    {
        var folder = Directory.CreateTempSubdirectory("estudarias-content-").FullName;
        _folders.Add(folder);
        foreach (var (name, yaml) in files)
        {
            File.WriteAllText(Path.Combine(folder, name), yaml);
        }

        return folder;
    }

    private async Task<ContentLoadResult> LoadAsync(string folder)
    {
        using var scope = NewScope();
        return await scope.Get<IContentLoader>().LoadAsync(folder, Cancel);
    }

    private static string RepositoryContentFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Estudarias.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "content");
    }

    /// <summary>Os ids de todo o conteúdo gravado, para provar que uma recarga não troca nenhum.</summary>
    private Task<List<Guid>> AllContentIdsAsync() => ReadAsync(async db =>
    {
        List<Guid> ids =
        [
            .. await db.Topics.Select(item => item.Id).ToListAsync(Cancel),
            .. await db.ErrorTypes.Select(item => item.Id).ToListAsync(Cancel),
            .. await db.Lessons.Select(item => item.Id).ToListAsync(Cancel),
            .. await db.Exercises.Select(item => item.Id).ToListAsync(Cancel),
            .. await db.Flashcards.Select(item => item.Id).ToListAsync(Cancel),
        ];
        ids.AddRange((await db.Exercises.ToListAsync(Cancel)).SelectMany(exercise => exercise.Alternatives).Select(item => item.Id));
        return ids.Order().ToList();
    });

    private static string Ordered(string firstExercise, string secondExercise) => $$"""
        slug: ordem
        name: Ordem
        errorTypes:
          - code: erro_a
            name: Erro A
            feedback: Feedback A.
        lessons:
          - order: 1
            title: Lição
            content: Texto.
            exercises:
        {{firstExercise}}
        {{secondExercise}}
        """;

    private const string ExerciseOne = """
              - code: primeiro
                statement: Pergunta 1
                difficulty: easy
                alternatives:
                  - text: "A"
                    errorType: erro_a
                  - text: "B"
                    correct: true
                  - text: "C"
                    errorType: erro_a
        """;

    private const string ExerciseTwo = """
              - code: segundo
                statement: Pergunta 2
                difficulty: hard
                alternatives:
                  - text: "X"
                    correct: true
                  - text: "Y"
                    errorType: erro_a
        """;

    private const string ExerciseOneReversed = """
              - code: primeiro
                statement: Pergunta 1
                difficulty: easy
                alternatives:
                  - text: "C"
                    errorType: erro_a
                  - text: "B"
                    correct: true
                  - text: "A"
                    errorType: erro_a
        """;

    private Task<List<(string Code, int Position, string[] Alternatives, int[] AlternativePositions)>> OrderedExercisesAsync() =>
        ReadAsync(async db =>
        {
            var exercises = await db.Exercises.ToListAsync(Cancel);
            return exercises
                .OrderBy(exercise => exercise.Position)
                .Select(exercise => (
                    exercise.Code,
                    exercise.Position,
                    exercise.Alternatives.Select(alternative => alternative.Text).ToArray(),
                    exercise.Alternatives.Select(alternative => alternative.Position).ToArray()))
                .ToList();
        });

    [Fact(DisplayName = "A carga grava a ordem do arquivo: a dos exercícios na lição e a das alternativas em cada exercício")]
    public async Task Load_RecordsTheOrderOfTheFile()
    {
        // Act
        var result = await LoadAsync(Folder(("ordem.yaml", Ordered(ExerciseOne, ExerciseTwo))));

        // Assert
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Problems));
        var exercises = await OrderedExercisesAsync();
        Assert.Equal(["primeiro", "segundo"], exercises.Select(exercise => exercise.Code));
        Assert.Equal([1, 2], exercises.Select(exercise => exercise.Position));
        Assert.Equal(["A", "B", "C"], exercises[0].Alternatives);
        Assert.Equal([1, 2, 3], exercises[0].AlternativePositions);
        Assert.Equal(["X", "Y"], exercises[1].Alternatives);
        Assert.Equal([1, 2], exercises[1].AlternativePositions);
    }

    [Fact(DisplayName = "Recarregar com a ordem trocada atualiza as posições e não troca nenhum id")]
    public async Task Load_WithTheOrderChanged_UpdatesThePositionsKeepingTheIds()
    {
        // Arrange
        await LoadAsync(Folder(("ordem.yaml", Ordered(ExerciseOne, ExerciseTwo))));
        var idsBefore = await AllContentIdsAsync();

        // Act: os exercícios trocam de lugar, e as alternativas do primeiro saem em ordem inversa
        var result = await LoadAsync(Folder(("ordem.yaml", Ordered(ExerciseTwo, ExerciseOneReversed))));

        // Assert
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal(0, result.Created);
        Assert.Equal(2, result.Updated);
        Assert.Equal(idsBefore, await AllContentIdsAsync());
        var exercises = await OrderedExercisesAsync();
        Assert.Equal(["segundo", "primeiro"], exercises.Select(exercise => exercise.Code));
        Assert.Equal([1, 2], exercises.Select(exercise => exercise.Position));
        Assert.Equal(["C", "B", "A"], exercises[1].Alternatives);
        Assert.Equal([1, 2, 3], exercises[1].AlternativePositions);
    }

    [Fact(DisplayName = "Recarregar o mesmo arquivo com a mesma ordem não altera nada")]
    public async Task Load_WithTheSameOrder_ChangesNothing()
    {
        // Arrange
        var folder = Folder(("ordem.yaml", Ordered(ExerciseOne, ExerciseTwo)));
        await LoadAsync(folder);

        // Act
        var result = await LoadAsync(folder);

        // Assert
        Assert.Equal((0, 0), (result.Created, result.Updated));
        Assert.Equal(5, result.Unchanged);
    }

    [Fact(DisplayName = "O conteúdo do repositório carrega inteiro: Operações básicas, Frações, Equações do 1º grau e Porcentagem, com tudo o que cada um tem")]
    public async Task Load_TheRepositoryContent_CreatesEverything()
    {
        // Act
        var result = await LoadAsync(RepositoryContentFolder());

        // Assert
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal((113, 0, 0), (result.Created, result.Updated, result.Unchanged));
        Assert.Equal(4, await ScalarAsync<long>("SELECT count(*) FROM topic"));
        Assert.Equal(27, await ScalarAsync<long>("SELECT count(*) FROM error_type"));
        Assert.Equal(12, await ScalarAsync<long>("SELECT count(*) FROM lesson"));
        Assert.Equal(37, await ScalarAsync<long>("SELECT count(*) FROM exercise"));
        Assert.Equal(128, await ScalarAsync<long>("SELECT count(*) FROM alternative"));
        Assert.Equal(33, await ScalarAsync<long>("SELECT count(*) FROM flashcard"));
        Assert.Equal(
            "operacoes-basicas",
            await ScalarAsync<string>(
                "SELECT p.slug FROM topic_prerequisite tp JOIN topic t ON t.id = tp.topic_id JOIN topic p ON p.id = tp.prerequisite_id WHERE t.slug = 'fracoes'"));
        Assert.Equal(
            "fracoes",
            await ScalarAsync<string>(
                "SELECT p.slug FROM topic_prerequisite tp JOIN topic t ON t.id = tp.topic_id JOIN topic p ON p.id = tp.prerequisite_id WHERE t.slug = 'equacoes-1-grau'"));
        Assert.Equal(
            "fracoes",
            await ScalarAsync<string>(
                "SELECT p.slug FROM topic_prerequisite tp JOIN topic t ON t.id = tp.topic_id JOIN topic p ON p.id = tp.prerequisite_id WHERE t.slug = 'porcentagem'"));
    }

    [Fact(DisplayName = "Recarregar o mesmo conteúdo não muda nada: nenhum item novo, nenhum id trocado, tudo igual no resumo")]
    public async Task Load_Twice_IsIdempotent()
    {
        // Arrange
        await LoadAsync(RepositoryContentFolder());
        var before = await AllContentIdsAsync();

        // Act
        var result = await LoadAsync(RepositoryContentFolder());

        // Assert
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal((0, 0, 113), (result.Created, result.Updated, result.Unchanged));
        Assert.Equal(before, await AllContentIdsAsync());
    }

    [Fact(DisplayName = "Revisar o YAML altera os itens mantendo os ids, e a alternativa nova entra com id próprio")]
    public async Task Load_ARevisedFile_UpdatesKeepingTheIds()
    {
        // Arrange
        await LoadAsync(Folder(("operacoes-basicas.yaml", Basics)));
        var before = await AllContentIdsAsync();
        var revised = Basics.ReplaceLineEndings("\n")
            .Replace("title: Soma", "title: Soma com reserva", StringComparison.Ordinal)
            .Replace("answer: Porque a dezena vai um.", "answer: Porque 7 + 5 passa de 9.", StringComparison.Ordinal)
            .Replace(
                "            errorType: esquece_o_vai_um\nflashcards:",
                "            errorType: esquece_o_vai_um\n          - text: \"52\"\n            errorType: esquece_o_vai_um\nflashcards:",
                StringComparison.Ordinal);
        Assert.Contains("\"52\"", revised, StringComparison.Ordinal);

        // Act
        var result = await LoadAsync(Folder(("operacoes-basicas.yaml", revised)));

        // Assert
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal((0, 3, 2), (result.Created, result.Updated, result.Unchanged));
        var after = await AllContentIdsAsync();
        Assert.Equal(before.Count + 1, after.Count);
        Assert.All(before, id => Assert.Contains(id, after));
        Assert.Equal("Soma com reserva", await ScalarAsync<string>("SELECT title FROM lesson"));
        Assert.Equal("Porque 7 + 5 passa de 9.", await ScalarAsync<string>("SELECT answer FROM flashcard"));
    }

    [Fact(DisplayName = "Um arquivo inválido não grava nada, nem dos arquivos válidos, e todos os problemas são listados")]
    public async Task Load_WithAnInvalidFile_WritesNothing()
    {
        // Arrange
        var invalid = """
            slug: fracoes
            name: Frações
            prerequisites: [nao-existe]
            lessons:
              - order: 0
                title: Lição
                content: Texto.
            """;

        // Act
        var result = await LoadAsync(Folder(("operacoes-basicas.yaml", Basics), ("fracoes.yaml", invalid)));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Problems.Count);
        Assert.All(result.Problems, problem => Assert.Equal("fracoes.yaml", problem.FileName));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM topic"));
    }

    [Fact(DisplayName = "Remover uma alternativa de um exercício já carregado é recusado, e nada da recarga é gravado")]
    public async Task Load_RemovingAnAlternative_WritesNothing()
    {
        // Arrange
        await LoadAsync(Folder(("operacoes-basicas.yaml", Basics)));
        var withoutAlternative = Basics.ReplaceLineEndings("\n")
            .Replace("title: Soma", "title: Outro título", StringComparison.Ordinal)
            .Replace("- text: \"32\"", "- text: \"12\"", StringComparison.Ordinal);
        Assert.Contains("\"12\"", withoutAlternative, StringComparison.Ordinal);

        // Act
        var result = await LoadAsync(Folder(("operacoes-basicas.yaml", withoutAlternative)));

        // Assert
        var problem = Assert.Single(result.Problems);
        Assert.Equal(("operacoes-basicas.yaml", "exercício 'soma_com_vai_um'"), (problem.FileName, problem.Item));
        Assert.Contains("exercise.alternative.removed", problem.Message, StringComparison.Ordinal);
        Assert.Equal("Soma", await ScalarAsync<string>("SELECT title FROM lesson"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM alternative"));
    }

    [Fact(DisplayName = "Um pré-requisito que já está no banco é aceito numa carga que não traz o arquivo dele")]
    public async Task Load_WithAPrerequisiteOnlyInTheDatabase_Succeeds()
    {
        // Arrange
        await LoadAsync(Folder(("operacoes-basicas.yaml", Basics)));
        var fractions = """
            slug: fracoes
            name: Frações
            prerequisites: [operacoes-basicas]
            """;

        // Act
        var result = await LoadAsync(Folder(("fracoes.yaml", fractions)));

        // Assert
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal((1, 0, 0), (result.Created, result.Updated, result.Unchanged));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM topic_prerequisite"));
    }
}
