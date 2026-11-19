using Estudarias.Domain.Answers;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Students;

namespace Estudarias.Domain.UnitTests.Answers;

public class AnswerDiagnosticianTests
{
    private static readonly Guid TopicId = Guid.CreateVersion7();

    private readonly ErrorType _sumsAcross = ErrorType.Create(
        TopicId,
        "soma_numeradores_e_denominadores",
        "Soma direta de numeradores e denominadores",
        null,
        "Para somar frações, primeiro deixe os denominadores iguais.");

    private readonly ErrorType _forgetsToSimplify = ErrorType.Create(
        TopicId, "nao_simplifica", "Não simplifica", null, "Divida numerador e denominador pelo MDC.");

    private readonly Alternative _correct = Alternative.Correct("5/6");
    private readonly Alternative _wrongSum;
    private readonly Alternative _wrongSimplify;
    private readonly Exercise _exercise;

    public AnswerDiagnosticianTests()
    {
        _wrongSum = Alternative.Wrong("2/5", _sumsAcross.Id);
        _wrongSimplify = Alternative.Wrong("10/12", _forgetsToSimplify.Id);
        _exercise = Exercise.Create(
            Guid.CreateVersion7(), "soma_de_meios_e_tercos", "Quanto é 1/2 + 1/3?", Difficulty.Easy, [_correct, _wrongSum, _wrongSimplify]);
    }

    [Fact(DisplayName = "A alternativa correta dá acerto, sem tipo de erro")]
    public void Diagnose_CorrectAlternative_IsCorrectWithoutErrorType()
    {
        // Act
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _correct.Id);

        // Assert
        Assert.Equal(new AnswerDiagnosis(true, _correct.Id, null), diagnosis);
    }

    [Fact(DisplayName = "Uma alternativa errada devolve o tipo de erro dela e a alternativa correta")]
    public void Diagnose_WrongAlternative_ReturnsItsErrorType()
    {
        // Act
        var sum = AnswerDiagnostician.Diagnose(_exercise, _wrongSum.Id);
        var simplify = AnswerDiagnostician.Diagnose(_exercise, _wrongSimplify.Id);

        // Assert
        Assert.Equal(new AnswerDiagnosis(false, _correct.Id, _sumsAcross.Id), sum);
        Assert.Equal(new AnswerDiagnosis(false, _correct.Id, _forgetsToSimplify.Id), simplify);
    }

    [Fact(DisplayName = "Uma alternativa de outro exercício lança answer.alternative.notInExercise")]
    public void Diagnose_AlternativeFromAnotherExercise_ThrowsDomainValidationException()
    {
        // Arrange
        var other = Exercise.Create(
            Guid.CreateVersion7(),
            "soma_de_quartos",
            "Quanto é 1/4 + 1/4?",
            Difficulty.Easy,
            [Alternative.Correct("1/2"), Alternative.Wrong("2/8", _sumsAcross.Id)]);
        var foreignId = other.Alternatives.First().Id;

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => AnswerDiagnostician.Diagnose(_exercise, foreignId));
        Assert.Equal("answer.alternative.notInExercise", exception.Code);
    }

    [Fact(DisplayName = "Um id de alternativa desconhecido também lança answer.alternative.notInExercise")]
    public void Diagnose_UnknownAlternative_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => AnswerDiagnostician.Diagnose(_exercise, Guid.CreateVersion7()));
        Assert.Equal("answer.alternative.notInExercise", exception.Code);
    }

    [Theory(DisplayName = "Um acerto dá retorno Correct nos dois grupos, com a resposta correta")]
    [InlineData(ExperimentalGroup.Control)]
    [InlineData(ExperimentalGroup.Diagnostic)]
    public void BuildFeedback_Correct_IsCorrectForBothGroups(ExperimentalGroup group)
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _correct.Id);

        // Act
        var feedback = AnswerDiagnostician.BuildFeedback(group, _exercise, diagnosis, null);

        // Assert
        Assert.Equal(new AnswerFeedback(AnswerFeedbackKind.Correct, "5/6", null, null), feedback);
    }

    [Fact(DisplayName = "Um erro no grupo diagnóstico traz o nome e o feedback do erro e a resposta correta")]
    public void BuildFeedback_WrongInDiagnosticGroup_IncludesErrorType()
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _wrongSum.Id);

        // Act
        var feedback = AnswerDiagnostician.BuildFeedback(ExperimentalGroup.Diagnostic, _exercise, diagnosis, _sumsAcross);

        // Assert
        Assert.Equal(
            new AnswerFeedback(
                AnswerFeedbackKind.IncorrectWithDiagnosis,
                "5/6",
                "Soma direta de numeradores e denominadores",
                "Para somar frações, primeiro deixe os denominadores iguais."),
            feedback);
    }

    [Fact(DisplayName = "Um erro no grupo controle traz só a resposta correta, sem nome nem feedback do erro")]
    public void BuildFeedback_WrongInControlGroup_OmitsErrorType()
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _wrongSum.Id);

        // Act
        var feedback = AnswerDiagnostician.BuildFeedback(ExperimentalGroup.Control, _exercise, diagnosis, _sumsAcross);

        // Assert
        Assert.Equal(new AnswerFeedback(AnswerFeedbackKind.Incorrect, "5/6", null, null), feedback);
    }

    [Fact(DisplayName = "No grupo controle, o tipo de erro pode vir nulo")]
    public void BuildFeedback_WrongInControlGroupWithoutErrorType_Succeeds()
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _wrongSum.Id);

        // Act
        var feedback = AnswerDiagnostician.BuildFeedback(ExperimentalGroup.Control, _exercise, diagnosis, null);

        // Assert
        Assert.Equal(AnswerFeedbackKind.Incorrect, feedback.Kind);
    }

    [Fact(DisplayName = "No grupo diagnóstico, um erro sem o tipo de erro lança ArgumentException")]
    public void BuildFeedback_WrongInDiagnosticGroupWithoutErrorType_ThrowsArgumentException()
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _wrongSum.Id);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => AnswerDiagnostician.BuildFeedback(ExperimentalGroup.Diagnostic, _exercise, diagnosis, null));
    }

    [Fact(DisplayName = "No grupo diagnóstico, um tipo de erro diferente do diagnóstico lança ArgumentException")]
    public void BuildFeedback_WrongInDiagnosticGroupWithOtherErrorType_ThrowsArgumentException()
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _wrongSum.Id);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => AnswerDiagnostician.BuildFeedback(
                ExperimentalGroup.Diagnostic, _exercise, diagnosis, _forgetsToSimplify));
    }

    [Fact(DisplayName = "Um grupo fora do enum lança ArgumentOutOfRangeException")]
    public void BuildFeedback_WithUndefinedGroup_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var diagnosis = AnswerDiagnostician.Diagnose(_exercise, _correct.Id);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AnswerDiagnostician.BuildFeedback((ExperimentalGroup)42, _exercise, diagnosis, null));
    }
}
