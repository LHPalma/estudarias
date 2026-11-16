using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;

namespace Estudarias.Domain.UnitTests.Exercises;

public class ExerciseTests
{
    private static readonly Guid LessonId = Guid.CreateVersion7();
    private static readonly Guid ErrorTypeId = Guid.CreateVersion7();

    private const string Statement = "Quanto é 1/2 + 1/3?";

    private static Alternative[] ValidAlternatives() =>
    [
        Alternative.Correct("5/6"),
        Alternative.Wrong("2/5", ErrorTypeId),
        Alternative.Wrong("2/6", ErrorTypeId),
    ];

    private static Exercise CreateWith(IReadOnlyCollection<Alternative> alternatives) =>
        Exercise.Create(LessonId, Statement, Difficulty.Easy, alternatives);

    [Fact(DisplayName = "Um exercício nasce com a lição, o enunciado, a dificuldade e as alternativas informados")]
    public void Create_WithValidData_KeepsValues()
    {
        // Arrange
        var alternatives = ValidAlternatives();

        // Act
        var exercise = Exercise.Create(LessonId, Statement, Difficulty.Medium, alternatives);

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
        var exercise = Exercise.Create(LessonId, statement, Difficulty.Easy, ValidAlternatives());

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
            () => Exercise.Create(Guid.Empty, Statement, Difficulty.Easy, ValidAlternatives()));
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
            () => Exercise.Create(LessonId, statement!, Difficulty.Easy, ValidAlternatives()));
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
            () => Exercise.Create(LessonId, Statement, (Difficulty)difficulty, ValidAlternatives()));
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
}
