using Estudarias.Application.Content;

namespace Estudarias.Application.UnitTests.Content;

public class ContentValidatorTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> NothingStored =
        new Dictionary<string, IReadOnlyCollection<string>>();

    private static TopicDocument Basics() =>
        new("operacoes-basicas", "Operações básicas", [], [], [], []);

    private static TopicDocument Fractions(
        IReadOnlyList<string>? prerequisites = null,
        IReadOnlyList<ErrorTypeDocument>? errorTypes = null,
        IReadOnlyList<LessonDocument>? lessons = null,
        IReadOnlyList<FlashcardDocument>? flashcards = null) =>
        new(
            "fracoes",
            "Frações",
            prerequisites ?? ["operacoes-basicas"],
            errorTypes ?? [new ErrorTypeDocument("soma_direta", "Soma direta", null, "Iguale os denominadores.")],
            lessons ?? [Lesson(1, Exercise("meio_mais_terco"))],
            flashcards ?? [new FlashcardDocument("por_que_nao_soma", "Por que não 2/5?", "Falta o denominador comum.", "soma_direta")]);

    private static LessonDocument Lesson(int order, params ExerciseDocument[] exercises) =>
        new(order, $"Lição {order}", "Para somar {1/2} + {1/3}, iguale os denominadores.", exercises);

    private static ExerciseDocument Exercise(string code, string difficulty = "easy", IReadOnlyList<AlternativeDocument>? alternatives = null) =>
        new(code, "Quanto é {1/2} + {1/3}?", difficulty, alternatives ??
        [
            new AlternativeDocument("{5/6}", true, null),
            new AlternativeDocument("{2/5}", false, "soma_direta"),
        ]);

    private static IReadOnlyList<ContentProblem> Validate(params TopicDocument[] topics) =>
        ContentValidator.Validate([.. topics.Select(topic => new ContentFile($"{topic.Slug}.yaml", topic))], NothingStored);

    private static ContentProblem Single(IReadOnlyList<ContentProblem> problems, string file, string item)
    {
        var problem = Assert.Single(problems);
        Assert.Equal((file, item), (problem.FileName, problem.Item));
        return problem;
    }

    [Fact(DisplayName = "Um conteúdo consistente não tem problema")]
    public void Validate_WithValidContent_ReturnsNoProblem()
    {
        // Act
        var problems = Validate(Basics(), Fractions());

        // Assert
        Assert.Empty(problems);
    }

    [Fact(DisplayName = "As regras do agregado valem na carga, com o arquivo, o item e o código do erro")]
    public void Validate_WithAnInvalidAggregate_ReportsTheDomainRule()
    {
        // Act
        var problems = Validate(Basics(), Fractions(lessons: [Lesson(1, Exercise("Meio_Mais_Terco"))]));

        // Assert
        var problem = Single(problems, "fracoes.yaml", "exercício 'Meio_Mais_Terco'");
        Assert.Contains("exercise.code.invalid", problem.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "O arquivo do tópico se chama <slug>.yaml")]
    public void Validate_WithAFileNamedAfterAnotherSlug_ReportsIt()
    {
        // Act
        var problems = ContentValidator.Validate(
            [new ContentFile("operacoes-basicas.yaml", Basics()), new ContentFile("fracoes-v2.yaml", Fractions())], NothingStored);

        // Assert
        var problem = Single(problems, "fracoes-v2.yaml", "tópico");
        Assert.Contains("fracoes.yaml", problem.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Um pré-requisito que não existe nos arquivos nem no banco é recusado")]
    public void Validate_WithAnUnknownPrerequisite_ReportsIt()
    {
        // Act
        var problems = Validate(Fractions(prerequisites: ["operacoes-basicas"]));

        // Assert
        Single(problems, "fracoes.yaml", "pré-requisito 'operacoes-basicas'");
    }

    [Fact(DisplayName = "Um pré-requisito que só existe no banco é aceito")]
    public void Validate_WithAPrerequisiteOnlyInTheDatabase_IsAccepted()
    {
        // Arrange
        var stored = new Dictionary<string, IReadOnlyCollection<string>> { ["operacoes-basicas"] = [] };

        // Act
        var problems = ContentValidator.Validate([new ContentFile("fracoes.yaml", Fractions())], stored);

        // Assert
        Assert.Empty(problems);
    }

    [Fact(DisplayName = "Um ciclo no grafo é recusado, com o caminho na mensagem")]
    public void Validate_WithACycle_ReportsThePath()
    {
        // Arrange
        var basics = Basics() with { Prerequisites = ["fracoes"] };

        // Act
        var problems = Validate(basics, Fractions());

        // Assert
        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.Equal("pré-requisitos", problem.Item));
        Assert.Contains(problems, problem => problem.Message.Contains("fracoes → operacoes-basicas → fracoes", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Um ciclo que passa por um tópico que só está no banco também é recusado")]
    public void Validate_WithACycleThroughTheDatabase_ReportsIt()
    {
        // Arrange
        var stored = new Dictionary<string, IReadOnlyCollection<string>> { ["operacoes-basicas"] = ["fracoes"] };

        // Act
        var problems = ContentValidator.Validate([new ContentFile("fracoes.yaml", Fractions())], stored);

        // Assert
        Single(problems, "fracoes.yaml", "pré-requisitos");
    }

    [Fact(DisplayName = "Um tópico não é pré-requisito de si mesmo")]
    public void Validate_WithASelfPrerequisite_ReportsIt()
    {
        // Act
        var problems = Validate(Basics(), Fractions(prerequisites: ["operacoes-basicas", "fracoes"]));

        // Assert
        Single(problems, "fracoes.yaml", "pré-requisito 'fracoes'");
    }

    [Fact(DisplayName = "Uma alternativa que aponta o tipo de erro de outro tópico é recusada, dizendo de qual tópico ele é")]
    public void Validate_WithAnErrorTypeOfAnotherTopic_ReportsItsTopic()
    {
        // Arrange
        var basics = Basics() with { ErrorTypes = [new ErrorTypeDocument("troca_sinal", "Troca o sinal", null, "Atenção ao sinal.")] };
        var exercise = Exercise("meio_mais_terco", alternatives:
        [
            new AlternativeDocument("{5/6}", true, null),
            new AlternativeDocument("{2/5}", false, "troca_sinal"),
        ]);

        // Act
        var problems = Validate(basics, Fractions(lessons: [Lesson(1, exercise)]));

        // Assert
        var problem = Single(problems, "fracoes.yaml", "alternativa '{2/5}' do exercício 'meio_mais_terco'");
        Assert.Contains("operacoes-basicas", problem.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Um flashcard que aponta um tipo de erro não declarado é recusado")]
    public void Validate_WithAFlashcardOfAnUnknownErrorType_ReportsIt()
    {
        // Act
        var problems = Validate(Basics(), Fractions(flashcards: [new FlashcardDocument("cartao", "Pergunta?", "Resposta.", "nao_existe")]));

        // Assert
        Single(problems, "fracoes.yaml", "flashcard 'cartao'");
    }

    [Fact(DisplayName = "Uma alternativa errada sem tipo de erro é recusada")]
    public void Validate_WithAWrongAlternativeWithoutErrorType_ReportsIt()
    {
        // Arrange
        var exercise = Exercise("meio_mais_terco", alternatives:
        [
            new AlternativeDocument("{5/6}", true, null),
            new AlternativeDocument("{2/5}", false, null),
        ]);

        // Act
        var problems = Validate(Basics(), Fractions(lessons: [Lesson(1, exercise)]));

        // Assert
        var problem = Single(problems, "fracoes.yaml", "alternativa '{2/5}' do exercício 'meio_mais_terco'");
        Assert.Contains("exercise.alternative.errorTypeRequired", problem.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Uma alternativa correta com tipo de erro é recusada")]
    public void Validate_WithACorrectAlternativeWithErrorType_ReportsIt()
    {
        // Arrange
        var exercise = Exercise("meio_mais_terco", alternatives:
        [
            new AlternativeDocument("{5/6}", true, "soma_direta"),
            new AlternativeDocument("{2/5}", false, "soma_direta"),
        ]);

        // Act
        var problems = Validate(Basics(), Fractions(lessons: [Lesson(1, exercise)]));

        // Assert
        Single(problems, "fracoes.yaml", "alternativa '{5/6}' do exercício 'meio_mais_terco'");
    }

    [Fact(DisplayName = "Uma dificuldade fora de easy, medium e hard é recusada")]
    public void Validate_WithAnUnknownDifficulty_ReportsIt()
    {
        // Act
        var problems = Validate(Basics(), Fractions(lessons: [Lesson(1, Exercise("meio_mais_terco", difficulty: "facil"))]));

        // Assert
        Single(problems, "fracoes.yaml", "exercício 'meio_mais_terco'");
    }

    [Fact(DisplayName = "O mesmo slug em dois arquivos é recusado nos dois, com os dois nomes")]
    public void Validate_WithTheSameSlugInTwoFiles_ReportsBoth()
    {
        // Act
        var problems = ContentValidator.Validate(
            [new ContentFile("operacoes-basicas.yaml", Basics()), new ContentFile("copia/operacoes-basicas.yaml", Basics())], NothingStored);

        // Assert
        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.Contains("copia/operacoes-basicas.yaml", problem.Message, StringComparison.Ordinal));
    }

    [Theory(DisplayName = "Uma chave natural repetida no tópico é recusada")]
    [InlineData("errorType", "tipo de erro 'soma_direta'")]
    [InlineData("lesson", "lição 1")]
    [InlineData("exercise", "exercício 'meio_mais_terco'")]
    [InlineData("flashcard", "flashcard 'por_que_nao_soma'")]
    public void Validate_WithARepeatedNaturalKey_ReportsIt(string kind, string item)
    {
        // Arrange
        var errorType = new ErrorTypeDocument("soma_direta", "Soma direta", null, "Iguale os denominadores.");
        var flashcard = new FlashcardDocument("por_que_nao_soma", "Por que não 2/5?", "Falta o denominador comum.", "soma_direta");
        var topic = kind switch
        {
            "errorType" => Fractions(errorTypes: [errorType, errorType]),
            "lesson" => Fractions(lessons: [Lesson(1, Exercise("meio_mais_terco")), Lesson(1, Exercise("outro"))]),
            "exercise" => Fractions(lessons: [Lesson(1, Exercise("meio_mais_terco")), Lesson(2, Exercise("meio_mais_terco"))]),
            _ => Fractions(flashcards: [flashcard, flashcard]),
        };

        // Act
        var problems = Validate(Basics(), topic);

        // Assert
        var problem = Single(problems, "fracoes.yaml", item);
        Assert.Contains("repetid", problem.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Todos os problemas são listados de uma vez, de todos os arquivos")]
    public void Validate_ListsEveryProblem()
    {
        // Arrange
        var basics = Basics() with { Name = " " };

        // Act
        var problems = Validate(basics, Fractions(lessons: [Lesson(0, Exercise("meio_mais_terco", difficulty: "facil"))]));

        // Assert
        Assert.Equal(3, problems.Count);
        Assert.Equal(["fracoes.yaml", "fracoes.yaml", "operacoes-basicas.yaml"], problems.Select(problem => problem.FileName).Order());
    }
}
