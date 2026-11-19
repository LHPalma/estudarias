using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Flashcards;

namespace Estudarias.Domain.UnitTests.Flashcards;

public class FlashcardTests
{
    private static readonly Guid TopicId = Guid.CreateVersion7();
    private static readonly Guid ErrorTypeId = Guid.CreateVersion7();

    private const string Code = "soma_direta";
    private const string Question = "Por que 1/2 + 1/3 não é 2/5?";
    private const string Answer = "Porque é preciso deixar os denominadores iguais antes de somar.";

    [Fact(DisplayName = "Um cartão manual nasce com origem manual, sem tipo de erro e com pergunta e resposta sem espaços nas pontas")]
    public void CreateManual_WithValidData_KeepsValues()
    {
        // Act
        var flashcard = Flashcard.CreateManual(TopicId, Code, $"  {Question}  ", $"\n{Answer}\n");

        // Assert
        Assert.Equal(TopicId, flashcard.TopicId);
        Assert.Equal(Question, flashcard.Question);
        Assert.Equal(Answer, flashcard.Answer);
        Assert.Equal(FlashcardOrigin.Manual, flashcard.Origin);
        Assert.Null(flashcard.ErrorTypeId);
    }

    [Fact(DisplayName = "Um cartão gerado por erro nasce com essa origem, o tipo de erro e pergunta e resposta sem espaços nas pontas")]
    public void CreateFromError_WithValidData_KeepsValues()
    {
        // Act
        var flashcard = Flashcard.CreateFromError(TopicId, Code, ErrorTypeId, $"  {Question}  ", $"\n{Answer}\n");

        // Assert
        Assert.Equal(TopicId, flashcard.TopicId);
        Assert.Equal(Question, flashcard.Question);
        Assert.Equal(Answer, flashcard.Answer);
        Assert.Equal(FlashcardOrigin.GeneratedByError, flashcard.Origin);
        Assert.Equal(ErrorTypeId, flashcard.ErrorTypeId);
    }

    [Fact(DisplayName = "O tópico do cartão manual é obrigatório")]
    public void CreateManual_WithEmptyTopic_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateManual(Guid.Empty, Code, Question, Answer));
        Assert.Equal("flashcard.topic.required", exception.Code);
    }

    [Fact(DisplayName = "O tópico do cartão gerado por erro é obrigatório")]
    public void CreateFromError_WithEmptyTopic_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(Guid.Empty, Code, ErrorTypeId, Question, Answer));
        Assert.Equal("flashcard.topic.required", exception.Code);
    }

    [Theory(DisplayName = "A pergunta do cartão manual é obrigatória")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void CreateManual_WithBlankQuestion_ThrowsDomainValidationException(string? question)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateManual(TopicId, Code, question!, Answer));
        Assert.Equal("flashcard.question.required", exception.Code);
    }

    [Theory(DisplayName = "A pergunta do cartão gerado por erro é obrigatória")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void CreateFromError_WithBlankQuestion_ThrowsDomainValidationException(string? question)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(TopicId, Code, ErrorTypeId, question!, Answer));
        Assert.Equal("flashcard.question.required", exception.Code);
    }

    [Theory(DisplayName = "A resposta do cartão manual é obrigatória")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void CreateManual_WithBlankAnswer_ThrowsDomainValidationException(string? answer)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateManual(TopicId, Code, Question, answer!));
        Assert.Equal("flashcard.answer.required", exception.Code);
    }

    [Theory(DisplayName = "A resposta do cartão gerado por erro é obrigatória")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void CreateFromError_WithBlankAnswer_ThrowsDomainValidationException(string? answer)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(TopicId, Code, ErrorTypeId, Question, answer!));
        Assert.Equal("flashcard.answer.required", exception.Code);
    }

    [Fact(DisplayName = "O cartão gerado por erro exige um tipo de erro")]
    public void CreateFromError_WithEmptyErrorType_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(TopicId, Code, Guid.Empty, Question, Answer));
        Assert.Equal("flashcard.errorType.required", exception.Code);
    }

    [Fact(DisplayName = "Cada cartão nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Act
        var first = Flashcard.CreateManual(TopicId, Code, Question, Answer);
        var second = Flashcard.CreateManual(TopicId, Code, Question, Answer);

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory(DisplayName = "O código do flashcard é obrigatório e tem o formato dos códigos de conteúdo")]
    [InlineData(null, "flashcard.code.required")]
    [InlineData("", "flashcard.code.required")]
    [InlineData("Soma", "flashcard.code.invalid")]
    [InlineData("soma direta", "flashcard.code.invalid")]
    public void Create_WithAnInvalidCode_ThrowsDomainValidationException(string? code, string expected)
    {
        // Act
        var manual = Assert.Throws<DomainValidationException>(() => Flashcard.CreateManual(TopicId, code!, Question, Answer));
        var generated = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(TopicId, code!, ErrorTypeId, Question, Answer));

        // Assert
        Assert.Equal((expected, expected), (manual.Code, generated.Code));
    }

    [Fact(DisplayName = "O cartão guarda o código")]
    public void Create_KeepsTheCode()
    {
        // Act
        var flashcard = Flashcard.CreateManual(TopicId, Code, Question, Answer);

        // Assert
        Assert.Equal(Code, flashcard.Code);
    }

    [Fact(DisplayName = "Revisar com tipo de erro torna o cartão gerado por erro; sem, manual")]
    public void Revise_ChangesTheOriginWithTheErrorType()
    {
        // Arrange
        var flashcard = Flashcard.CreateManual(TopicId, Code, Question, Answer);

        // Act
        flashcard.Revise("  Outra pergunta  ", "Outra resposta", ErrorTypeId);
        var generated = (flashcard.Question, flashcard.Answer, flashcard.Origin, flashcard.ErrorTypeId);
        flashcard.Revise(Question, Answer, errorTypeId: null);

        // Assert
        Assert.Equal(("Outra pergunta", "Outra resposta", FlashcardOrigin.GeneratedByError, (Guid?)ErrorTypeId), generated);
        Assert.Equal((FlashcardOrigin.Manual, (Guid?)null, Code), (flashcard.Origin, flashcard.ErrorTypeId, flashcard.Code));
    }

    [Fact(DisplayName = "Revisar aplica as regras da criação, e nada muda quando falha")]
    public void Revise_WithAnEmptyQuestion_ThrowsAndChangesNothing()
    {
        // Arrange
        var flashcard = Flashcard.CreateManual(TopicId, Code, Question, Answer);

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => flashcard.Revise(" ", Answer, ErrorTypeId));

        // Assert
        Assert.Equal("flashcard.question.required", exception.Code);
        Assert.Equal((Question, FlashcardOrigin.Manual), (flashcard.Question, flashcard.Origin));
    }
}
