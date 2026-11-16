using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;

namespace Estudarias.Domain.UnitTests.Exercises;

public class AlternativeTests
{
    private static readonly Guid ErrorTypeId = Guid.CreateVersion7();

    [Fact(DisplayName = "Uma alternativa correta nasce com o texto (sem espaços nas pontas) e sem tipo de erro")]
    public void Correct_KeepsTrimmedTextWithoutErrorType()
    {
        // Act
        var alternative = Alternative.Correct("  5/6  ");

        // Assert
        Assert.Equal("5/6", alternative.Text);
        Assert.True(alternative.IsCorrect);
        Assert.Null(alternative.ErrorTypeId);
    }

    [Fact(DisplayName = "Uma alternativa errada nasce com o texto (sem espaços nas pontas) e o tipo de erro")]
    public void Wrong_KeepsTrimmedTextAndErrorType()
    {
        // Act
        var alternative = Alternative.Wrong("  2/5  ", ErrorTypeId);

        // Assert
        Assert.Equal("2/5", alternative.Text);
        Assert.False(alternative.IsCorrect);
        Assert.Equal(ErrorTypeId, alternative.ErrorTypeId);
    }

    [Theory(DisplayName = "O texto da alternativa correta é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Correct_WithBlankText_ThrowsDomainValidationException(string? text)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Alternative.Correct(text!));
        Assert.Equal("exercise.alternative.textRequired", exception.Code);
    }

    [Theory(DisplayName = "O texto da alternativa errada é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Wrong_WithBlankText_ThrowsDomainValidationException(string? text)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Alternative.Wrong(text!, ErrorTypeId));
        Assert.Equal("exercise.alternative.textRequired", exception.Code);
    }

    [Fact(DisplayName = "A alternativa errada exige um tipo de erro")]
    public void Wrong_WithEmptyErrorType_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Alternative.Wrong("2/5", Guid.Empty));
        Assert.Equal("exercise.alternative.errorTypeRequired", exception.Code);
    }

    [Fact(DisplayName = "Cada alternativa nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Act
        var first = Alternative.Correct("5/6");
        var second = Alternative.Correct("5/6");

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }
}
