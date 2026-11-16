using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Students;

namespace Estudarias.Domain.Answers;

/// <summary>
/// Corrige a resposta do aluno e monta o que ele vê conforme o grupo experimental: o grupo diagnóstico vê o nome e o
/// feedback do tipo de erro, o grupo controle vê só que errou. É a diferença que o experimento mede.
/// </summary>
public static class AnswerDiagnostician
{
    /// <summary>Diz se a alternativa escolhida está certa e, se não estiver, o tipo de erro que ela revela.</summary>
    public static AnswerDiagnosis Diagnose(Exercise exercise, Guid alternativeId)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var chosen = exercise.Alternatives.FirstOrDefault(alternative => alternative.Id == alternativeId)
            ?? throw new DomainValidationException(
                AnswerErrors.AlternativeNotInExercise, "The chosen alternative does not belong to the exercise.");

        var correct = CorrectAlternativeOf(exercise);

        return new AnswerDiagnosis(chosen.IsCorrect, correct.Id, chosen.ErrorTypeId);
    }

    /// <summary>Monta o retorno ao aluno conforme o grupo experimental.</summary>
    /// <param name="errorType">
    /// O tipo de erro do diagnóstico. Obrigatório quando um aluno do grupo diagnóstico erra; ignorado nos demais casos.
    /// </param>
    public static AnswerFeedback BuildFeedback(
        ExperimentalGroup group, Exercise exercise, AnswerDiagnosis diagnosis, ErrorType? errorType)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(diagnosis);

        if (!Enum.IsDefined(group))
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Experimental group is not valid.");
        }

        var correctAnswer = CorrectAlternativeOf(exercise).Text;

        if (diagnosis.IsCorrect)
        {
            return new AnswerFeedback(AnswerFeedbackKind.Correct, correctAnswer, null, null);
        }

        if (group == ExperimentalGroup.Control)
        {
            return new AnswerFeedback(AnswerFeedbackKind.Incorrect, correctAnswer, null, null);
        }

        if (errorType is null || errorType.Id != diagnosis.ErrorTypeId)
        {
            throw new ArgumentException(
                "The diagnostic group needs the error type revealed by the diagnosis.", nameof(errorType));
        }

        return new AnswerFeedback(
            AnswerFeedbackKind.IncorrectWithDiagnosis, correctAnswer, errorType.Name, errorType.Feedback);
    }

    // O Exercise garante exatamente uma alternativa correta.
    private static Alternative CorrectAlternativeOf(Exercise exercise) =>
        exercise.Alternatives.Single(alternative => alternative.IsCorrect);
}
