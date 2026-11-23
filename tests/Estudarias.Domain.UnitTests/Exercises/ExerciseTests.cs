using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;

namespace Estudarias.Domain.UnitTests.Exercises;

public class ExerciseTests
{
    private static readonly Guid LessonId = Guid.CreateVersion7();
    private static readonly Guid ErrorTypeId = Guid.CreateVersion7();

    private const string Code = "soma_de_fracoes";
    private const string Statement = "Quanto é 1/2 + 1/3?";

    private static Alternative[] ValidAlternatives() =>
    [
        Alternative.Correct("5/6"),
        Alternative.Wrong("2/5", ErrorTypeId),
        Alternative.Wrong("2/6", ErrorTypeId),
    ];

    private static Exercise CreateWith(IReadOnlyCollection<Alternative> alternatives) =>
        Exercise.Create(LessonId, 1, Code, Statement, Difficulty.Easy, alternatives);

    [Fact(DisplayName = "Um exercício nasce com a lição, o enunciado, a dificuldade e as alternativas informados")]
    public void Create_WithValidData_KeepsValues()
    {
        // Arrange
        var alternatives = ValidAlternatives();

        // Act
        var exercise = Exercise.Create(LessonId, 1, Code, Statement, Difficulty.Medium, alternatives);

        // Assert
        Assert.Equal(LessonId, exercise.LessonId);
        Assert.Equal(Statement, exercise.Statement);
        Assert.Equal(Difficulty.Medium, exercise.Difficulty);
        Assert.Equal(alternatives, exercise.Alternatives);
    }

    [Fact(DisplayName = "O enunciado é guardado como veio, sem remover os espaços e as quebras das pontas")]
    public void Create_KeepsStatementAsGiven()
    {
        // Arrange
        var statement = "\n  Quanto é 1/2 + 1/3?\n";

        // Act
        var exercise = Exercise.Create(LessonId, 1, Code, statement, Difficulty.Easy, ValidAlternatives());

        // Assert
        Assert.Equal(statement, exercise.Statement);
    }

    [Fact(DisplayName = "Mudar a coleção passada depois da criação não muda as alternativas do exercício")]
    public void Create_CopiesAlternatives()
    {
        // Arrange
        var alternatives = ValidAlternatives().ToList();
        var exercise = CreateWith(alternatives);

        // Act
        alternatives.Clear();

        // Assert
        Assert.Equal(3, exercise.Alternatives.Count);
    }

    [Fact(DisplayName = "Um exercício com 2 alternativas, 1 correta e 1 errada, é aceito")]
    public void Create_WithTwoAlternatives_Succeeds()
    {
        // Act
        var exercise = CreateWith([Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId)]);

        // Assert
        Assert.Equal(2, exercise.Alternatives.Count);
    }

    [Fact(DisplayName = "A lição do exercício é obrigatória")]
    public void Create_WithEmptyLesson_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Exercise.Create(Guid.Empty, 1, Code, Statement, Difficulty.Easy, ValidAlternatives()));
        Assert.Equal("exercise.lesson.required", exception.Code);
    }

    [Theory(DisplayName = "O enunciado do exercício é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Create_WithBlankStatement_ThrowsDomainValidationException(string? statement)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Exercise.Create(LessonId, 1, Code, statement!, Difficulty.Easy, ValidAlternatives()));
        Assert.Equal("exercise.statement.required", exception.Code);
    }

    [Theory(DisplayName = "A dificuldade precisa ser um valor definido")]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void Create_WithUndefinedDifficulty_ThrowsDomainValidationException(int difficulty)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Exercise.Create(LessonId, 1, Code, Statement, (Difficulty)difficulty, ValidAlternatives()));
        Assert.Equal("exercise.difficulty.invalid", exception.Code);
    }

    [Fact(DisplayName = "Um exercício sem alternativas é rejeitado")]
    public void Create_WithoutAlternatives_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWith([]));
        Assert.Equal("exercise.alternative.tooFew", exception.Code);
    }

    [Fact(DisplayName = "Um exercício com só 1 alternativa é rejeitado")]
    public void Create_WithSingleAlternative_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWith([Alternative.Correct("5/6")]));
        Assert.Equal("exercise.alternative.tooFew", exception.Code);
    }

    [Fact(DisplayName = "Um exercício sem alternativa correta é rejeitado")]
    public void Create_WithoutCorrectAlternative_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateWith([Alternative.Wrong("2/5", ErrorTypeId), Alternative.Wrong("2/6", ErrorTypeId)]));
        Assert.Equal("exercise.alternative.noCorrect", exception.Code);
    }

    [Fact(DisplayName = "Um exercício com duas alternativas corretas é rejeitado")]
    public void Create_WithTwoCorrectAlternatives_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateWith(
                [Alternative.Correct("5/6"), Alternative.Correct("10/12"), Alternative.Wrong("2/5", ErrorTypeId)]));
        Assert.Equal("exercise.alternative.multipleCorrect", exception.Code);
    }

    [Theory(DisplayName = "O texto das alternativas não se repete no exercício, ignorando maiúsculas")]
    [InlineData("2/5", "2/5")]
    [InlineData("Dois quintos", "dois quintos")]
    [InlineData("FRAÇÃO", "fração")]
    public void Create_WithDuplicatedText_ThrowsDomainValidationException(string first, string second)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateWith(
                [Alternative.Correct("5/6"), Alternative.Wrong(first, ErrorTypeId), Alternative.Wrong(second, ErrorTypeId)]));
        Assert.Equal("exercise.alternative.textDuplicated", exception.Code);
    }

    [Fact(DisplayName = "Uma alternativa errada com o mesmo texto da correta também é repetição")]
    public void Create_WithWrongEqualToCorrect_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateWith([Alternative.Correct("5/6"), Alternative.Wrong(" 5/6 ", ErrorTypeId)]));
        Assert.Equal("exercise.alternative.textDuplicated", exception.Code);
    }

    [Fact(DisplayName = "Cada exercício nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Act
        var first = CreateWith(ValidAlternatives());
        var second = CreateWith(ValidAlternatives());

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory(DisplayName = "O código do exercício é obrigatório e tem o formato dos códigos de conteúdo")]
    [InlineData(null, "exercise.code.required")]
    [InlineData("   ", "exercise.code.required")]
    [InlineData("Soma", "exercise.code.invalid")]
    [InlineData("1_soma", "exercise.code.invalid")]
    [InlineData("soma-de-fracoes", "exercise.code.invalid")]
    [InlineData("soma__fracoes", "exercise.code.invalid")]
    public void Create_WithAnInvalidCode_ThrowsDomainValidationException(string? code, string expected)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Exercise.Create(LessonId, 1, code!, Statement, Difficulty.Easy, ValidAlternatives()));
        Assert.Equal(expected, exception.Code);
    }

    [Fact(DisplayName = "Revisar troca a lição, o enunciado e a dificuldade, e mantém o código")]
    public void Revise_ChangesTheContentAndKeepsTheCode()
    {
        // Arrange
        var exercise = CreateWith(ValidAlternatives());
        var otherLesson = Guid.CreateVersion7();

        // Act
        exercise.Revise(otherLesson, 1, "Quanto é 1/3 + 1/2?", Difficulty.Hard, ValidAlternatives());

        // Assert
        Assert.Equal(
            (otherLesson, "Quanto é 1/3 + 1/2?", Difficulty.Hard, Code),
            (exercise.LessonId, exercise.Statement, exercise.Difficulty, exercise.Code));
    }

    [Fact(DisplayName = "Revisar mantém o id da alternativa de mesmo texto, sem diferenciar maiúsculas, e assume a correção nova")]
    public void Revise_KeepsTheIdOfAlternativesWithTheSameText()
    {
        // Arrange
        var exercise = CreateWith([Alternative.Correct("5/6"), Alternative.Wrong("Dois quintos", ErrorTypeId)]);
        var correctId = exercise.Alternatives.Single(alternative => alternative.IsCorrect).Id;
        var wrongId = exercise.Alternatives.Single(alternative => !alternative.IsCorrect).Id;
        var otherErrorType = Guid.CreateVersion7();

        // Act
        exercise.Revise(
            LessonId, 1, Statement, Difficulty.Easy, [Alternative.Wrong("5/6", otherErrorType), Alternative.Correct("dois QUINTOS")]);

        // Assert
        var formerCorrect = exercise.Alternatives.Single(alternative => alternative.Id == correctId);
        var formerWrong = exercise.Alternatives.Single(alternative => alternative.Id == wrongId);
        Assert.Equal((false, (Guid?)otherErrorType), (formerCorrect.IsCorrect, formerCorrect.ErrorTypeId));
        Assert.Equal(("dois QUINTOS", true, (Guid?)null), (formerWrong.Text, formerWrong.IsCorrect, formerWrong.ErrorTypeId));
        Assert.Equal(2, exercise.Alternatives.Count);
    }

    [Fact(DisplayName = "Revisar acrescenta a alternativa nova com id próprio")]
    public void Revise_AddsNewAlternatives()
    {
        // Arrange
        var exercise = CreateWith([Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId)]);
        var ids = exercise.Alternatives.Select(alternative => alternative.Id).ToList();

        // Act
        exercise.Revise(
            LessonId, 1,
            Statement,
            Difficulty.Easy,
            [Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId), Alternative.Wrong("1/6", ErrorTypeId)]);

        // Assert
        Assert.Equal(3, exercise.Alternatives.Count);
        Assert.Equal(ids, exercise.Alternatives.Take(2).Select(alternative => alternative.Id));
        Assert.DoesNotContain(exercise.Alternatives.Single(alternative => alternative.Text == "1/6").Id, ids);
    }

    [Fact(DisplayName = "Revisar sem uma alternativa que já existia é recusado, e nada muda")]
    public void Revise_RemovingAnAlternative_ThrowsAndChangesNothing()
    {
        // Arrange
        var exercise = CreateWith(ValidAlternatives());

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => exercise.Revise(
            Guid.CreateVersion7(), 1, "Outro", Difficulty.Hard, [Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId)]));

        // Assert
        Assert.Equal("exercise.alternative.removed", exception.Code);
        Assert.Equal(
            (LessonId, Statement, Difficulty.Easy, 3),
            (exercise.LessonId, exercise.Statement, exercise.Difficulty, exercise.Alternatives.Count));
    }

    [Fact(DisplayName = "Revisar aplica as regras das alternativas da criação, e nada muda quando falha")]
    public void Revise_WithTwoCorrect_ThrowsAndChangesNothing()
    {
        // Arrange
        var exercise = CreateWith(ValidAlternatives());

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => exercise.Revise(
            LessonId, 1,
            Statement,
            Difficulty.Easy,
            [Alternative.Correct("5/6"), Alternative.Correct("2/5"), Alternative.Wrong("2/6", ErrorTypeId)]));

        // Assert
        Assert.Equal("exercise.alternative.multipleCorrect", exception.Code);
        Assert.Equal("5/6", Assert.Single(exercise.Alternatives, alternative => alternative.IsCorrect).Text);
    }

    // ---------- A ordem do conteúdo (#104) ----------

    [Fact(DisplayName = "Um exercício guarda o lugar dele na lição")]
    public void Create_KeepsThePositionInTheLesson()
    {
        // Act
        var exercise = Exercise.Create(LessonId, 3, Code, Statement, Difficulty.Easy, ValidAlternatives());

        // Assert
        Assert.Equal(3, exercise.Position);
    }

    [Theory(DisplayName = "A posição do exercício começa em 1")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithAPositionBelowOne_Throws(int position)
    {
        // Act
        var exception = Assert.Throws<DomainValidationException>(
            () => Exercise.Create(LessonId, position, Code, Statement, Difficulty.Easy, ValidAlternatives()));

        // Assert
        Assert.Equal("exercise.position.invalid", exception.Code);
    }

    [Fact(DisplayName = "As alternativas são numeradas a partir de 1, na ordem em que chegam")]
    public void Create_NumbersTheAlternativesInTheGivenOrder()
    {
        // Arrange
        var exercise = CreateWith([Alternative.Wrong("2/5", ErrorTypeId), Alternative.Correct("5/6"), Alternative.Wrong("2/6", ErrorTypeId)]);

        // Act
        var alternatives = exercise.Alternatives.ToList();

        // Assert
        Assert.Equal(["2/5", "5/6", "2/6"], alternatives.Select(alternative => alternative.Text));
        Assert.Equal([1, 2, 3], alternatives.Select(alternative => alternative.Position));
    }

    [Fact(DisplayName = "A lista de alternativas sai na ordem do conteúdo, e não na do id")]
    public void Alternatives_AreOrderedByPositionNotById()
    {
        // Arrange: a correta é criada por último, então o id dela é o maior, e ela vem no meio do conteúdo
        var first = Alternative.Wrong("2/5", ErrorTypeId);
        var second = Alternative.Wrong("2/6", ErrorTypeId);
        var correct = Alternative.Correct("5/6");

        // Act
        var exercise = CreateWith([first, correct, second]);

        // Assert
        Assert.Equal(["2/5", "5/6", "2/6"], exercise.Alternatives.Select(alternative => alternative.Text));
    }

    [Fact(DisplayName = "Revisar com as alternativas em outra ordem muda as posições e mantém os ids")]
    public void Revise_ReordersTheAlternativesKeepingTheirIds()
    {
        // Arrange
        var exercise = CreateWith([Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId), Alternative.Wrong("2/6", ErrorTypeId)]);
        var idByText = exercise.Alternatives.ToDictionary(alternative => alternative.Text, alternative => alternative.Id);

        // Act
        exercise.Revise(
            LessonId, 1, Statement, Difficulty.Easy,
            [Alternative.Wrong("2/6", ErrorTypeId), Alternative.Wrong("2/5", ErrorTypeId), Alternative.Correct("5/6")]);

        // Assert
        var alternatives = exercise.Alternatives.ToList();
        Assert.Equal(["2/6", "2/5", "5/6"], alternatives.Select(alternative => alternative.Text));
        Assert.Equal([1, 2, 3], alternatives.Select(alternative => alternative.Position));
        Assert.All(alternatives, alternative => Assert.Equal(idByText[alternative.Text], alternative.Id));
    }

    [Fact(DisplayName = "Revisar com uma alternativa nova no meio a põe no lugar que a lista dá, e as outras acompanham")]
    public void Revise_InsertsANewAlternativeAtItsPlace()
    {
        // Arrange
        var exercise = CreateWith([Alternative.Correct("5/6"), Alternative.Wrong("2/6", ErrorTypeId)]);

        // Act
        exercise.Revise(
            LessonId, 1, Statement, Difficulty.Easy,
            [Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId), Alternative.Wrong("2/6", ErrorTypeId)]);

        // Assert
        Assert.Equal(["5/6", "2/5", "2/6"], exercise.Alternatives.Select(alternative => alternative.Text));
        Assert.Equal([1, 2, 3], exercise.Alternatives.Select(alternative => alternative.Position));
    }

    [Fact(DisplayName = "Revisar muda o lugar do exercício na lição")]
    public void Revise_ChangesThePosition()
    {
        // Arrange
        var exercise = CreateWith(ValidAlternatives());

        // Act
        exercise.Revise(LessonId, 4, Statement, Difficulty.Easy, ValidAlternatives());

        // Assert
        Assert.Equal(4, exercise.Position);
    }

    [Fact(DisplayName = "Revisar com posição inválida é recusado, e a ordem das alternativas não muda")]
    public void Revise_WithAPositionBelowOne_ThrowsAndChangesNothing()
    {
        // Arrange
        var exercise = CreateWith([Alternative.Correct("5/6"), Alternative.Wrong("2/5", ErrorTypeId)]);

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => exercise.Revise(
            LessonId, 0, Statement, Difficulty.Easy, [Alternative.Wrong("2/5", ErrorTypeId), Alternative.Correct("5/6")]));

        // Assert
        Assert.Equal("exercise.position.invalid", exception.Code);
        Assert.Equal(1, exercise.Position);
        Assert.Equal(["5/6", "2/5"], exercise.Alternatives.Select(alternative => alternative.Text));
    }
}
