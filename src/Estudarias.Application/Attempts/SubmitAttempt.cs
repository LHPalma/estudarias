using Estudarias.Application.Common;
using Estudarias.Application.ErrorTypes;
using Estudarias.Application.Exercises;
using Estudarias.Application.Flashcards;
using Estudarias.Application.Identity;
using Estudarias.Application.StudentFlashcards;
using Estudarias.Application.Students;
using Estudarias.Application.Topics;
using Estudarias.Application.Trail;
using Estudarias.Application.Trail.Queries;
using Estudarias.Domain.Answers;
using Estudarias.Domain.Attempts;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Mastery;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Domain.Students;

namespace Estudarias.Application.Attempts;

/// <param name="ExerciseId">O exercício da rota.</param>
/// <param name="AlternativeId">A alternativa escolhida. Ausente na requisição, chega como <see cref="Guid.Empty"/> e falha como qualquer alternativa que não é do exercício.</param>
/// <param name="ResponseSeconds">O tempo até responder, opcional e não negativo. Só vai para a análise do experimento.</param>
public sealed record SubmitAttemptCommand(Guid ExerciseId, Guid AlternativeId, int? ResponseSeconds);

/// <summary>O tópico do exercício depois da resposta: o status, a nota e o total de tentativas já com a resposta que acabou de chegar.</summary>
/// <param name="Score">Com uma casa, arredondada para baixo (<c>TopicScore.Display</c>).</param>
public sealed record AttemptTopicResult(Guid Id, TopicStatus Status, decimal Score, int AttemptCount);

/// <summary>
/// A lição do exercício depois da resposta, já com ela: o estado e quantos exercícios o aluno acertou, de quantos. É o que a tela de fim
/// de lição usa para mostrar "Lição concluída" só quando for verdade (RN-TRL-12).
/// </summary>
public sealed record AttemptLessonResult(Guid Id, LessonStatus Status, int CompletedExercises, int TotalExercises);

/// <summary>Um tópico liberado por esta resposta, para o aviso de desbloqueio.</summary>
public sealed record UnlockedTopicResult(Guid Id, string Name);

/// <summary>O cartão de revisão que este erro criou: o id do cartão do aluno (é ele que a revisão usa) e a primeira revisão.</summary>
public sealed record CreatedFlashcardResult(Guid Id, DateTimeOffset NextReviewAt);

/// <summary>O que o aluno vê depois de responder. Sem texto pronto: as mensagens ("Resposta correta") são do app (RN-RSP-07).</summary>
public sealed record SubmitAttemptResult(
    Guid AttemptId,
    bool IsCorrect,
    Guid CorrectAlternativeId,
    AnswerFeedback Feedback,
    AttemptTopicResult Topic,
    AttemptLessonResult Lesson,
    IReadOnlyList<UnlockedTopicResult> UnlockedTopics,
    CreatedFlashcardResult? Flashcard);

/// <summary>
/// O ciclo do produto numa chamada: <b>errar, diagnosticar, flashcard, nota, desbloqueio</b>. Corrige a resposta, grava a tentativa,
/// recalcula a nota do tópico já com ela e avalia o domínio e o desbloqueio, cria o cartão de revisão do erro (para os dois grupos,
/// ADR-005) e monta o retorno conforme o grupo. Tentativa, progresso e cartão são gravados juntos: ou todos entram, ou nenhum
/// (RN-RSP-12). O aluno é sempre o dono do token, e o grupo vem do aluno gravado, nunca da requisição (RN-RSP-13).
/// </summary>
public sealed class SubmitAttemptHandler(
    IUserRepository users,
    IStudentRepository students,
    IExerciseRepository exercises,
    ITopicRepository topics,
    IErrorTypeRepository errorTypes,
    IFlashcardRepository flashcards,
    IStudentFlashcardRepository studentFlashcards,
    IAttemptRepository attempts,
    IAnswerReads answers,
    ILessonProgressReads lessonProgress,
    IReviewScheduler scheduler,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <summary>O índice único do cartão do aluno, que fecha a corrida de dois erros do mesmo tipo (FA-RSP-10).</summary>
    public const string StudentFlashcardUniqueIndex = "ux_student_flashcard_student_id_flashcard_id";

    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainAuthenticationException">A conta não existe, está desabilitada ou não tem aluno.</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainNotFoundException">O exercício não existe (<c>trail.exercise.notFound</c>).</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainForbiddenException">O tópico do exercício está bloqueado (<c>student.topic.locked</c>).</exception>
    /// <exception cref="Estudarias.Domain.Common.Exceptions.DomainValidationException">
    /// A alternativa não é do exercício (<c>answer.alternative.notInExercise</c>) ou o tempo é negativo (<c>attempt.responseTime.invalid</c>).
    /// </exception>
    public async Task<SubmitAttemptResult> Handle(Guid userId, SubmitAttemptCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is not { IsActive: true, StudentId: { } studentId })
        {
            throw IdentityErrors.InvalidSession();
        }

        var student = await students.FindAsync(studentId, cancellationToken)
            ?? throw IdentityErrors.InvalidSession();

        var found = await exercises.FindAsync(command.ExerciseId, cancellationToken)
            ?? throw TrailErrors.NoSuchExercise();
        var exercise = found.Exercise;
        var topicId = found.TopicId;

        // Um tópico bloqueado não se pratica, e a recusa vem antes de qualquer gravação (RN-RSP-01).
        if (student.StatusOf(topicId) == TopicStatus.Locked)
        {
            throw StudentErrors.LockedTopic();
        }

        // A alternativa que não é do exercício falha aqui (RN-RSP-02), e o tempo negativo na criação da tentativa (RN-RSP-03).
        var diagnosis = AnswerDiagnostician.Diagnose(exercise, command.AlternativeId);
        var attempt = Attempt.Create(student.Id, exercise.Id, command.AlternativeId, command.ResponseSeconds, clock);

        // A nota considera a resposta que acabou de chegar (RN-RSP-08): ela ainda não está no banco, então entra na frente das
        // anteriores, e a janela é a das 10 mais recentes. O total de tentativas é o anterior mais esta.
        var previous = await answers.OfTopicAsync(student.Id, topicId, cancellationToken);
        var window = new List<GradedAnswer>(TopicScore.Window) { new(exercise.Difficulty, diagnosis.IsCorrect) };
        window.AddRange(previous.Latest.Take(TopicScore.Window - 1));
        var attemptCount = previous.Total + 1;
        var score = TopicScore.Compute(window);

        // O estado da lição também considera esta resposta, que ainda não está no banco: o exercício entra entre os tentados e, se
        // acertou, entre os acertados. Errar um exercício já acertado não desfaz a conclusão (RN-TRL-13).
        var lessonBefore = await lessonProgress.OfLessonAsync(student.Id, exercise.LessonId, cancellationToken);
        var lessonNow = LessonProgress.Of(
            lessonBefore.ExerciseIds,
            [.. lessonBefore.Attempted, exercise.Id],
            diagnosis.IsCorrect ? [.. lessonBefore.Correct, exercise.Id] : lessonBefore.Correct);

        var allTopics = await topics.ListAsync(cancellationToken);
        var unlocked = TopicMastery.Evaluate(student, topicId, score, attemptCount, allTopics, clock);

        // O retorno por grupo exige o tipo de erro quando o aluno do diagnóstico erra; sem ele é defeito de dados (RN-RSP-05), e o
        // erro acontece antes de gravar qualquer coisa.
        ErrorType? errorType = null;
        if (!diagnosis.IsCorrect && student.Group == ExperimentalGroup.Diagnostic)
        {
            errorType = await errorTypes.FindAsync(diagnosis.ErrorTypeId!.Value, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"The error type {diagnosis.ErrorTypeId} revealed by the alternative does not exist.");
        }

        var feedback = AnswerDiagnostician.BuildFeedback(student.Group, exercise, diagnosis, errorType);

        // Errar cria o cartão do tipo de erro, uma vez por aluno e por tipo, para os dois grupos (ADR-005, RN-RSP-10).
        var card = diagnosis.IsCorrect
            ? null
            : await NewCardAsync(student.Id, diagnosis.ErrorTypeId!.Value, attempt.Id, cancellationToken);

        attempts.Add(attempt);
        if (card is not null)
        {
            studentFlashcards.Add(card);
        }

        card = await SaveAsync(card, cancellationToken);

        return new SubmitAttemptResult(
            attempt.Id,
            diagnosis.IsCorrect,
            diagnosis.CorrectAlternativeId,
            feedback,
            new AttemptTopicResult(topicId, student.StatusOf(topicId), score.Display, attemptCount),
            new AttemptLessonResult(exercise.LessonId, lessonNow.Status, lessonNow.CompletedExercises, lessonNow.TotalExercises),
            [.. unlocked.Select(id => new UnlockedTopicResult(id, allTopics.Single(topic => topic.Id == id).Name))],
            card is null ? null : new CreatedFlashcardResult(card.Id, card.NextReviewAt));
    }

    /// <summary>
    /// O cartão do flashcard gerado por aquele tipo de erro, ou nulo quando o aluno já o tem (o erro repetido não cria outro nem
    /// reinicia o agendamento) ou quando o conteúdo não tem flashcard para o tipo de erro (PEND-RSP-02).
    /// </summary>
    private async Task<StudentFlashcard?> NewCardAsync(
        Guid studentId, Guid errorTypeId, Guid attemptId, CancellationToken cancellationToken)
    {
        var flashcard = await flashcards.FindGeneratedByAsync(errorTypeId, cancellationToken);
        if (flashcard is null || await studentFlashcards.ExistsAsync(studentId, flashcard.Id, cancellationToken))
        {
            return null;
        }

        return StudentFlashcard.Create(studentId, flashcard.Id, attemptId, scheduler, clock);
    }

    /// <summary>
    /// Grava tudo numa unidade de trabalho. Se outra requisição do mesmo aluno criou o mesmo cartão no meio do caminho, o índice único
    /// recusa o segundo, e esta resposta descarta o cartão dela e grava o resto: a tentativa não se perde, e o cartão que vale é o da
    /// outra (FA-RSP-10). Qualquer outra violação segue como erro.
    /// </summary>
    private async Task<StudentFlashcard?> SaveAsync(StudentFlashcard? card, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return card;
        }
        catch (UniqueConstraintViolationException exception)
            when (card is not null && exception.ConstraintName == StudentFlashcardUniqueIndex)
        {
            studentFlashcards.Discard(card);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }
    }
}
