using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Flashcards;

namespace Estudarias.Domain.UnitTests.Flashcards;

public class FlashcardTests
{
    private static readonly Guid TopicId = Guid.CreateVersion7();
    private static readonly Guid ErrorTypeId = Guid.CreateVersion7();

    private const string Question = "Por que 1/2 + 1/3 não é 2/5?";
    private const string Answer = "Porque é preciso deixar os denominadores iguais antes de somar.";

    [Fact(DisplayName = "Um cartão manual nasce com origem manual, sem tipo de erro e com pergunta e resposta sem espaços nas pontas")]
    public void CreateManual_WithValidData_KeepsValues()
    {
        // Act
        var flashcard = Flashcard.CreateManual(TopicId, $"  {Question}  ", $"\n{Answer}\n");

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
        var flashcard = Flashcard.CreateFromError(TopicId, ErrorTypeId, $"  {Question}  ", $"\n{Answer}\n");

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
            () => Flashcard.CreateManual(Guid.Empty, Question, Answer));
        Assert.Equal("flashcard.topic.required", exception.Code);
    }

    [Fact(DisplayName = "O tópico do cartão gerado por erro é obrigatório")]
    public void CreateFromError_WithEmptyTopic_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(Guid.Empty, ErrorTypeId, Question, Answer));
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
            () => Flashcard.CreateManual(TopicId, question!, Answer));
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
            () => Flashcard.CreateFromError(TopicId, ErrorTypeId, question!, Answer));
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
            () => Flashcard.CreateManual(TopicId, Question, answer!));
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
            () => Flashcard.CreateFromError(TopicId, ErrorTypeId, Question, answer!));
        Assert.Equal("flashcard.answer.required", exception.Code);
    }

    [Fact(DisplayName = "O cartão gerado por erro exige um tipo de erro")]
    public void CreateFromError_WithEmptyErrorType_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Flashcard.CreateFromError(TopicId, Guid.Empty, Question, Answer));
        Assert.Equal("flashcard.errorType.required", exception.Code);
    }

    [Fact(DisplayName = "Cada cartão nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Act
        var first = Flashcard.CreateManual(TopicId, Question, Answer);
        var second = Flashcard.CreateManual(TopicId, Question, Answer);

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }
}
