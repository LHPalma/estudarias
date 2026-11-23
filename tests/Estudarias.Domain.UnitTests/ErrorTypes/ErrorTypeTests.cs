using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.ErrorTypes;

namespace Estudarias.Domain.UnitTests.ErrorTypes;

public class ErrorTypeTests
{
    private static readonly Guid TopicId = Guid.CreateVersion7();

    private const string Code = "soma_numeradores_e_denominadores";
    private const string Name = "Soma direta de numeradores e denominadores";
    private const string Feedback = "Para somar frações, primeiro deixe os denominadores iguais.";

    private static ErrorType CreateWithCode(string code) =>
        ErrorType.Create(TopicId, code, Name, null, Feedback);

    [Fact(DisplayName = "Um tipo de erro nasce com o tópico, o código, o nome (sem espaços nas pontas) e o feedback informados")]
    public void Create_WithValidData_KeepsValues()
    {
        // Act
        var errorType = ErrorType.Create(TopicId, Code, $"  {Name}  ", null, Feedback);

        // Assert
        Assert.Equal(TopicId, errorType.TopicId);
        Assert.Equal(Code, errorType.Code);
        Assert.Equal(Name, errorType.Name);
        Assert.Null(errorType.Description);
        Assert.Equal(Feedback, errorType.Feedback);
    }

    [Fact(DisplayName = "A descrição preenchida é guardada sem espaços nas pontas")]
    public void Create_WithDescription_KeepsTrimmedDescription()
    {
        // Act
        var errorType = ErrorType.Create(TopicId, Code, Name, "  Soma 1/2 + 1/3 como 2/5.  ", Feedback);

        // Assert
        Assert.Equal("Soma 1/2 + 1/3 como 2/5.", errorType.Description);
    }

    [Theory(DisplayName = "A descrição em branco vira nula")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithBlankDescription_StoresNull(string description)
    {
        // Act
        var errorType = ErrorType.Create(TopicId, Code, Name, description, Feedback);

        // Assert
        Assert.Null(errorType.Description);
    }

    [Fact(DisplayName = "O feedback é guardado como veio, sem remover os espaços das pontas")]
    public void Create_KeepsFeedbackAsGiven()
    {
        // Arrange
        var feedback = "  Passo 1: ache o MMC.\nPasso 2: some os numeradores.\n";

        // Act
        var errorType = ErrorType.Create(TopicId, Code, Name, null, feedback);

        // Assert
        Assert.Equal(feedback, errorType.Feedback);
    }

    [Fact(DisplayName = "O tópico do tipo de erro é obrigatório")]
    public void Create_WithEmptyTopic_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => ErrorType.Create(Guid.Empty, Code, Name, null, Feedback));
        Assert.Equal("errorType.topic.required", exception.Code);
    }

    [Theory(DisplayName = "O código do tipo de erro é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ThrowsDomainValidationException(string? code)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWithCode(code!));
        Assert.Equal("errorType.code.required", exception.Code);
    }

    [Theory(DisplayName = "Códigos no formato de minúsculas e dígitos com _ entre grupos são aceitos")]
    [InlineData("inverte_fracao")]
    [InlineData("soma_numeradores_e_denominadores")]
    [InlineData("erro")]
    [InlineData("erro2")]
    [InlineData("sinal_trocado_1_grau")]
    public void Create_WithWellFormedCode_Succeeds(string code)
    {
        // Act
        var errorType = CreateWithCode(code);

        // Assert
        Assert.Equal(code, errorType.Code);
    }

    [Theory(DisplayName = "O código precisa ter só minúsculas e dígitos, com _ entre grupos, começando por letra")]
    [InlineData("Inverte_fracao")]
    [InlineData("inverte fracao")]
    [InlineData(" inverte_fracao")]
    [InlineData("inverte_fracao ")]
    [InlineData("inverte_fracao\n")]
    [InlineData("inverte-fracao")]
    [InlineData("_inverte_fracao")]
    [InlineData("inverte_fracao_")]
    [InlineData("inverte__fracao")]
    [InlineData("inverte_fração")]
    [InlineData("1_inverte_fracao")]
    public void Create_WithMalformedCode_ThrowsDomainValidationException(string code)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWithCode(code));
        Assert.Equal("errorType.code.invalid", exception.Code);
    }

    [Fact(DisplayName = "O código do tipo de erro não passa de 100 caracteres")]
    public void Create_WithCodeLongerThanLimit_ThrowsDomainValidationException()
    {
        // Arrange
        var code = new string('a', 101);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWithCode(code));
        Assert.Equal("errorType.code.invalid", exception.Code);
    }

    [Fact(DisplayName = "Um código com exatamente 100 caracteres é aceito")]
    public void Create_WithCodeAtLimit_Succeeds()
    {
        // Arrange
        var code = new string('a', 100);

        // Act
        var errorType = CreateWithCode(code);

        // Assert
        Assert.Equal(code, errorType.Code);
    }

    [Theory(DisplayName = "O nome do tipo de erro é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithBlankName_ThrowsDomainValidationException(string? name)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => ErrorType.Create(TopicId, Code, name!, null, Feedback));
        Assert.Equal("errorType.name.required", exception.Code);
    }

    [Fact(DisplayName = "O nome do tipo de erro não passa de 100 caracteres")]
    public void Create_WithNameLongerThanLimit_ThrowsDomainValidationException()
    {
        // Arrange
        var name = new string('a', 101);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => ErrorType.Create(TopicId, Code, name, null, Feedback));
        Assert.Equal("errorType.name.tooLong", exception.Code);
    }

    [Fact(DisplayName = "O limite do nome vale depois de remover os espaços das pontas")]
    public void Create_WithNameAtLimitPlusPadding_Succeeds()
    {
        // Arrange
        var name = new string('a', 100);

        // Act
        var errorType = ErrorType.Create(TopicId, Code, $"  {name}  ", null, Feedback);

        // Assert
        Assert.Equal(name, errorType.Name);
    }

    [Theory(DisplayName = "O feedback do tipo de erro é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void Create_WithBlankFeedback_ThrowsDomainValidationException(string? feedback)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => ErrorType.Create(TopicId, Code, Name, null, feedback!));
        Assert.Equal("errorType.feedback.required", exception.Code);
    }

    [Fact(DisplayName = "Cada tipo de erro nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Act
        var first = CreateWithCode("inverte_fracao");
        var second = CreateWithCode("inverte_fracao");

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact(DisplayName = "Revisar troca o nome, a descrição e o feedback, e mantém o tópico e o código")]
    public void Revise_ChangesTheContentAndKeepsTheKey()
    {
        // Arrange
        var errorType = ErrorType.Create(TopicId, Code, Name, "Descrição", Feedback);

        // Act
        errorType.Revise("  Inverte a fração  ", "   ", "Feedback novo");

        // Assert
        Assert.Equal(
            (TopicId, Code, "Inverte a fração", (string?)null, "Feedback novo"),
            (errorType.TopicId, errorType.Code, errorType.Name, errorType.Description, errorType.Feedback));
    }

    [Fact(DisplayName = "Revisar aplica as regras da criação, e nada muda quando falha")]
    public void Revise_WithoutFeedback_ThrowsAndChangesNothing()
    {
        // Arrange
        var errorType = ErrorType.Create(TopicId, Code, Name, null, Feedback);

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => errorType.Revise("Outro nome", null, " "));

        // Assert
        Assert.Equal("errorType.feedback.required", exception.Code);
        Assert.Equal(Name, errorType.Name);
    }
}
