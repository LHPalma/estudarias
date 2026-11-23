using Estudarias.Api.Links;
using Estudarias.Application.Attempts;
using Estudarias.Domain.Answers;

namespace Estudarias.Api.Contracts.Attempts;

/// <summary>O tipo de erro do diagnóstico: só o grupo diagnóstico o recebe, e só quando errou.</summary>
public sealed record AttemptErrorTypeResponse(string Name, string Feedback);

/// <summary>
/// O retorno ao aluno, sem texto pronto: as mensagens ("Resposta correta") são do app. <c>Kind</c> é <c>correct</c>, <c>incorrect</c>
/// ou <c>incorrectWithDiagnosis</c>; <c>ErrorType</c> é nulo fora do último. A estrutura é a mesma nos dois grupos (RN-RSP-05).
/// </summary>
public sealed record AttemptFeedbackResponse(string Kind, string CorrectAnswer, AttemptErrorTypeResponse? ErrorType);

/// <summary>O tópico do exercício depois da resposta, já com ela: status, nota (uma casa, para baixo) e o total de tentativas.</summary>
public sealed record AttemptTopicResponse(Guid Id, string Status, decimal Score, int AttemptCount);

/// <summary>
/// A lição do exercício depois da resposta, já com ela: <c>Status</c> é <c>notStarted</c>, <c>inProgress</c> ou <c>completed</c>, e
/// <c>CompletedExercises</c> de <c>TotalExercises</c> diz quantos exercícios o aluno já acertou. A tela de fim de lição mostra "Lição
/// concluída" só quando for verdade.
/// </summary>
public sealed record AttemptLessonResponse(Guid Id, string Status, int CompletedExercises, int TotalExercises);

/// <summary>Um tópico que esta resposta liberou.</summary>
public sealed record AttemptUnlockedTopicResponse(Guid Id, string Name);

/// <summary>O cartão de revisão que este erro criou: o id do cartão do aluno (o que <c>POST /reviews/{id}</c> usa) e a primeira revisão.</summary>
public sealed record AttemptFlashcardResponse(Guid Id, DateTimeOffset NextReviewAt);

/// <summary>
/// O que o aluno precisa para a tela do retorno de uma vez: a correção, o retorno do grupo, a nota nova, os tópicos liberados e o
/// cartão criado (<c>null</c> se acertou, se já tinha o cartão do tipo de erro ou se o conteúdo não tem flashcard para ele).
/// </summary>
public sealed record AttemptResponse(
    Guid AttemptId,
    bool IsCorrect,
    Guid CorrectAlternativeId,
    AttemptFeedbackResponse Feedback,
    AttemptTopicResponse Topic,
    AttemptLessonResponse Lesson,
    IReadOnlyList<AttemptUnlockedTopicResponse> UnlockedTopics,
    AttemptFlashcardResponse? Flashcard,
    IReadOnlyList<Link> Links)
{
    public static AttemptResponse From(SubmitAttemptResult result, IReadOnlyList<Link> links) =>
        new(
            result.AttemptId,
            result.IsCorrect,
            result.CorrectAlternativeId,
            new AttemptFeedbackResponse(
                KindText(result.Feedback.Kind),
                result.Feedback.CorrectAnswer,
                result.Feedback.ErrorTypeName is { } name && result.Feedback.ErrorTypeFeedback is { } feedback
                    ? new AttemptErrorTypeResponse(name, feedback)
                    : null),
            new AttemptTopicResponse(result.Topic.Id, TopicStatusText.Of(result.Topic.Status), result.Topic.Score, result.Topic.AttemptCount),
            new AttemptLessonResponse(
                result.Lesson.Id,
                LessonStatusText.Of(result.Lesson.Status),
                result.Lesson.CompletedExercises,
                result.Lesson.TotalExercises),
            [.. result.UnlockedTopics.Select(topic => new AttemptUnlockedTopicResponse(topic.Id, topic.Name))],
            result.Flashcard is { } card ? new AttemptFlashcardResponse(card.Id, card.NextReviewAt) : null,
            links);

    // Explícito, e não o ToString() do enum: o texto é contrato com o app.
    private static string KindText(AnswerFeedbackKind kind) => kind switch
    {
        AnswerFeedbackKind.Correct => "correct",
        AnswerFeedbackKind.Incorrect => "incorrect",
        AnswerFeedbackKind.IncorrectWithDiagnosis => "incorrectWithDiagnosis",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Feedback kind is not valid."),
    };
}
