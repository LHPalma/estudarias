using Estudarias.Application.Attempts;
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
using Estudarias.Application.UnitTests.Support;
using Estudarias.Domain.Answers;
using Estudarias.Domain.Attempts;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Estudarias.Application.UnitTests.Attempts;

public class SubmitAttemptHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly IExerciseRepository _exercises = Substitute.For<IExerciseRepository>();
    private readonly ITopicRepository _topics = Substitute.For<ITopicRepository>();
    private readonly IErrorTypeRepository _errorTypes = Substitute.For<IErrorTypeRepository>();
    private readonly IFlashcardRepository _flashcards = Substitute.For<IFlashcardRepository>();
    private readonly IStudentFlashcardRepository _studentFlashcards = Substitute.For<IStudentFlashcardRepository>();
    private readonly IAttemptRepository _attempts = Substitute.For<IAttemptRepository>();
    private readonly IAnswerReads _answers = Substitute.For<IAnswerReads>();
    private readonly ILessonProgressReads _lessonProgress = Substitute.For<ILessonProgressReads>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Start);

    private readonly Topic _basics = Topic.Create("Operações básicas", "operacoes-basicas");
    private readonly Topic _fractions = Topic.Create("Frações", "fracoes");
    private readonly ErrorType _errorType;
    private readonly Flashcard _flashcard;
    private readonly Exercise _exercise;
    private readonly Alternative _correct;
    private readonly Alternative _wrong;
    private readonly Student _student;
    private readonly User _user;

    private readonly List<Attempt> _addedAttempts = [];
    private readonly List<StudentFlashcard> _addedCards = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public SubmitAttemptHandlerTests()
    {
        _fractions.AddPrerequisite(_basics.Id);

        _errorType = ErrorType.Create(_basics.Id, "esquece_o_vai_um", "Esquece o vai um", null, "A dezena vai para a esquerda.");
        _flashcard = Flashcard.CreateFromError(_basics.Id, "por_que_vai_um", _errorType.Id, "Por que 27 + 15 não é 32?", "A dezena vai um.");

        _correct = Alternative.Correct("42");
        _wrong = Alternative.Wrong("32", _errorType.Id);
        _exercise = Exercise.Create(Guid.CreateVersion7(), 1, "soma_com_vai_um", "Quanto é 27 + 15?", Difficulty.Medium, [_correct, _wrong]);

        _student = Student.Create("Ana", ExperimentalGroup.Diagnostic);
        _student.Unlock(_basics.Id, _clock);
        _user = User.RegisterStudent("ana@example.com", TestUsers.Hash, _student.Id);

        _users.FindByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _students.FindAsync(_student.Id, Arg.Any<CancellationToken>()).Returns(_student);
        _exercises.FindAsync(_exercise.Id, Arg.Any<CancellationToken>()).Returns(new ExerciseInTopic(_exercise, _basics.Id));
        _topics.ListAsync(Arg.Any<CancellationToken>()).Returns([_basics, _fractions]);
        _errorTypes.FindAsync(_errorType.Id, Arg.Any<CancellationToken>()).Returns(_errorType);
        _flashcards.FindGeneratedByAsync(_errorType.Id, Arg.Any<CancellationToken>()).Returns(_flashcard);
        _studentFlashcards.ExistsAsync(_student.Id, _flashcard.Id, Arg.Any<CancellationToken>()).Returns(false);
        _answers.OfTopicAsync(_student.Id, _basics.Id, Arg.Any<CancellationToken>()).Returns((Previous(), 0));
        WithLessonBefore([_exercise.Id], attempted: [], correct: []);

        _attempts.When(repository => repository.Add(Arg.Any<Attempt>())).Do(call => _addedAttempts.Add(call.Arg<Attempt>()));
        _studentFlashcards.When(repository => repository.Add(Arg.Any<StudentFlashcard>())).Do(call => _addedCards.Add(call.Arg<StudentFlashcard>()));
    }

    private SubmitAttemptHandler Handler() => new(
        _users, _students, _exercises, _topics, _errorTypes, _flashcards, _studentFlashcards, _attempts, _answers,
        _lessonProgress, new Sm2Scheduler(), _unitOfWork, _clock);

    private void WithLessonBefore(Guid[] exerciseIds, Guid[] attempted, Guid[] correct) =>
        _lessonProgress.OfLessonAsync(_student.Id, _exercise.LessonId, Arg.Any<CancellationToken>())
            .Returns(new LessonAnswers(exerciseIds, attempted, correct));

    private static IReadOnlyList<GradedAnswer> Previous(params GradedAnswer[] mostRecentFirst) => mostRecentFirst;

    private static GradedAnswer Right() => new(Difficulty.Medium, IsCorrect: true);

    private static GradedAnswer Wrong() => new(Difficulty.Medium, IsCorrect: false);

    private void WithPrevious(int total, params GradedAnswer[] mostRecentFirst) =>
        _answers.OfTopicAsync(_student.Id, _basics.Id, Arg.Any<CancellationToken>()).Returns((Previous(mostRecentFirst), total));

    private Task<SubmitAttemptResult> AnswerAsync(Alternative alternative, int? seconds = 12) =>
        Handler().Handle(_user.Id, new SubmitAttemptCommand(_exercise.Id, alternative.Id, seconds), Cancel);

    private void AssertNothingWasWritten()
    {
        Assert.Empty(_addedAttempts);
        Assert.Empty(_addedCards);
        _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Acertar grava a tentativa, devolve o retorno correct e não cria cartão")]
    public async Task Handle_WithTheRightAlternative_RecordsTheAttemptWithoutACard()
    {
        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.True(result.IsCorrect);
        Assert.Equal(_correct.Id, result.CorrectAlternativeId);
        Assert.Equal(AnswerFeedbackKind.Correct, result.Feedback.Kind);
        Assert.Equal("42", result.Feedback.CorrectAnswer);
        Assert.Null(result.Flashcard);
        Assert.Empty(_addedCards);
        var attempt = Assert.Single(_addedAttempts);
        Assert.Equal((_student.Id, _exercise.Id, _correct.Id, 12, Start), (attempt.StudentId, attempt.ExerciseId, attempt.AlternativeId, attempt.ResponseSeconds, attempt.AnsweredAt));
        Assert.Equal(result.AttemptId, attempt.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
        await _flashcards.DidNotReceive().FindGeneratedByAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Errar no grupo diagnóstico devolve o tipo de erro e cria o cartão, com a tentativa de origem e o primeiro agendamento")]
    public async Task Handle_WithAWrongAlternative_InTheDiagnosticGroup_ReturnsTheDiagnosisAndCreatesTheCard()
    {
        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.False(result.IsCorrect);
        Assert.Equal(_correct.Id, result.CorrectAlternativeId);
        Assert.Equal(AnswerFeedbackKind.IncorrectWithDiagnosis, result.Feedback.Kind);
        Assert.Equal(("Esquece o vai um", "A dezena vai para a esquerda."), (result.Feedback.ErrorTypeName, result.Feedback.ErrorTypeFeedback));
        var card = Assert.Single(_addedCards);
        Assert.Equal((_student.Id, _flashcard.Id, Assert.Single(_addedAttempts).Id), (card.StudentId, card.FlashcardId, card.OriginAttemptId));
        Assert.Equal(new CreatedFlashcardResult(card.Id, card.NextReviewAt), result.Flashcard);
        Assert.Equal(Sm2Scheduler.AlgorithmName, card.Algorithm);
        Assert.True(card.NextReviewAt > Start);
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "ADR-005: errar no grupo controle devolve só incorrect, sem o tipo de erro, e cria o mesmo cartão")]
    public async Task Handle_WithAWrongAlternative_InTheControlGroup_HidesTheDiagnosisButCreatesTheCard()
    {
        // Arrange
        var control = Student.Create("Bia", ExperimentalGroup.Control);
        control.Unlock(_basics.Id, _clock);
        var user = User.RegisterStudent("bia@example.com", TestUsers.Hash, control.Id);
        _users.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _students.FindAsync(control.Id, Arg.Any<CancellationToken>()).Returns(control);
        _answers.OfTopicAsync(control.Id, _basics.Id, Arg.Any<CancellationToken>()).Returns((Previous(), 0));
        _lessonProgress.OfLessonAsync(control.Id, _exercise.LessonId, Arg.Any<CancellationToken>())
            .Returns(new LessonAnswers([_exercise.Id], [], []));
        _studentFlashcards.ExistsAsync(control.Id, _flashcard.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await Handler().Handle(user.Id, new SubmitAttemptCommand(_exercise.Id, _wrong.Id, null), Cancel);

        // Assert
        Assert.Equal(AnswerFeedbackKind.Incorrect, result.Feedback.Kind);
        Assert.Null(result.Feedback.ErrorTypeName);
        Assert.Null(result.Feedback.ErrorTypeFeedback);
        Assert.Equal("42", result.Feedback.CorrectAnswer);
        Assert.NotNull(result.Flashcard);
        Assert.Equal(control.Id, Assert.Single(_addedCards).StudentId);
        await _errorTypes.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Errar de novo o mesmo tipo de erro não cria outro cartão, e a resposta traz flashcard nulo")]
    public async Task Handle_WhenTheStudentAlreadyHasTheCard_DoesNotCreateAnother()
    {
        // Arrange
        _studentFlashcards.ExistsAsync(_student.Id, _flashcard.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.Null(result.Flashcard);
        Assert.Empty(_addedCards);
        Assert.Single(_addedAttempts);
        await _unitOfWork.Received(1).SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "Um tipo de erro sem flashcard gerado no conteúdo responde com flashcard nulo, sem falhar")]
    public async Task Handle_WhenTheErrorTypeHasNoFlashcard_ReturnsNull()
    {
        // Arrange
        _flashcards.FindGeneratedByAsync(_errorType.Id, Arg.Any<CancellationToken>()).Returns((Flashcard?)null);

        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.Null(result.Flashcard);
        Assert.Empty(_addedCards);
        Assert.Single(_addedAttempts);
    }

    [Fact(DisplayName = "A nota considera a resposta que acabou de chegar, na frente das anteriores, e o total de tentativas a inclui")]
    public async Task Handle_ComputesTheScoreIncludingTheNewAnswer()
    {
        // Arrange: uma resposta errada antes, e esta acerta, as duas médias: 100 × 2 / (2 + 1,8) = 52,63
        WithPrevious(1, Wrong());

        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal((52.6m, 2), (result.Topic.Score, result.Topic.AttemptCount));
        Assert.Equal((_basics.Id, TopicStatus.Available), (result.Topic.Id, result.Topic.Status));
    }

    [Fact(DisplayName = "A janela da nota tem 10 respostas: a nova mais as 9 mais recentes, e as mais antigas não contam")]
    public async Task Handle_TheScoreWindowKeepsOnlyTenAnswers()
    {
        // Arrange: nove certas recentes e uma errada mais antiga (que cairia fora da janela com a nova); mais 5 erradas ainda antigas
        var latest = Enumerable.Repeat(Right(), 9).Append(Wrong()).ToArray();
        WithPrevious(15, latest);

        // Act
        var result = await AnswerAsync(_correct);

        // Assert: só a nova e as 9 certas entram, então a nota é 100, e o total é 16
        Assert.Equal((100.0m, 16), (result.Topic.Score, result.Topic.AttemptCount));
    }

    [Fact(DisplayName = "A resposta que domina o tópico o marca como dominado e devolve os dependentes liberados")]
    public async Task Handle_WhenTheAnswerMastersTheTopic_UnlocksTheDependents()
    {
        // Arrange: quatro acertos antes, e este é o quinto, com nota 100
        WithPrevious(4, Right(), Right(), Right(), Right());

        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal((TopicStatus.Mastered, 5, 100.0m), (result.Topic.Status, result.Topic.AttemptCount, result.Topic.Score));
        var unlocked = Assert.Single(result.UnlockedTopics);
        Assert.Equal((_fractions.Id, "Frações"), (unlocked.Id, unlocked.Name));
        Assert.Equal(TopicStatus.Mastered, _student.StatusOf(_basics.Id));
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_fractions.Id));
    }

    [Fact(DisplayName = "Com a nota alta mas menos de 5 tentativas, o tópico não é dominado e nada é liberado")]
    public async Task Handle_WithAGoodScoreButFewAttempts_DoesNotMaster()
    {
        // Arrange
        WithPrevious(3, Right(), Right(), Right());

        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal((TopicStatus.Available, 4), (result.Topic.Status, result.Topic.AttemptCount));
        Assert.Empty(result.UnlockedTopics);
    }

    [Fact(DisplayName = "Responder um tópico dominado é permitido e não rebaixa o status")]
    public async Task Handle_OnAMasteredTopic_KeepsItMastered()
    {
        // Arrange
        _student.MarkMastered(_basics.Id, _clock);
        WithPrevious(5, Right(), Right(), Right(), Right(), Right());

        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.Equal(TopicStatus.Mastered, result.Topic.Status);
        Assert.Equal(6, result.Topic.AttemptCount);
        Assert.Single(_addedAttempts);
    }

    [Fact(DisplayName = "RN-RSP-01: um tópico bloqueado é student.topic.locked (403), e nada é gravado")]
    public async Task Handle_OnALockedTopic_ThrowsForbiddenAndWritesNothing()
    {
        // Arrange
        _exercises.FindAsync(_exercise.Id, Arg.Any<CancellationToken>()).Returns(new ExerciseInTopic(_exercise, _fractions.Id));

        // Act
        var exception = await Assert.ThrowsAsync<DomainForbiddenException>(() => AnswerAsync(_correct));

        // Assert
        Assert.Equal(StudentErrors.TopicLocked, exception.Code);
        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "RN-RSP-02: uma alternativa de outro exercício é answer.alternative.notInExercise, e nada é gravado")]
    public async Task Handle_WithAnAlternativeOfAnotherExercise_ThrowsAndWritesNothing()
    {
        // Act
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() =>
            Handler().Handle(_user.Id, new SubmitAttemptCommand(_exercise.Id, Guid.CreateVersion7(), null), Cancel));

        // Assert
        Assert.Equal(AnswerErrors.AlternativeNotInExercise, exception.Code);
        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "Sem alternativa na requisição (Guid vazio), falha como qualquer alternativa que não é do exercício")]
    public async Task Handle_WithAnEmptyAlternative_ThrowsLikeAnyUnknownAlternative()
    {
        // Act
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() =>
            Handler().Handle(_user.Id, new SubmitAttemptCommand(_exercise.Id, Guid.Empty, null), Cancel));

        // Assert
        Assert.Equal(AnswerErrors.AlternativeNotInExercise, exception.Code);
    }

    [Fact(DisplayName = "RN-RSP-03: um tempo de resposta negativo é attempt.responseTime.invalid, e nada é gravado")]
    public async Task Handle_WithANegativeResponseTime_ThrowsAndWritesNothing()
    {
        // Act
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => AnswerAsync(_correct, seconds: -1));

        // Assert
        Assert.Equal(AttemptErrors.ResponseTimeInvalid, exception.Code);
        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "O tempo de resposta pode faltar e pode ser zero")]
    public async Task Handle_TheResponseTimeIsOptional()
    {
        // Act
        await AnswerAsync(_correct, seconds: null);
        await AnswerAsync(_correct, seconds: 0);

        // Assert
        Assert.Equal([(int?)null, 0], _addedAttempts.Select(attempt => attempt.ResponseSeconds));
    }

    [Fact(DisplayName = "FA-RSP-06: um exercício que não existe é trail.exercise.notFound (404), e nada é gravado")]
    public async Task Handle_WithAnUnknownExercise_ThrowsNotFound()
    {
        // Act
        var exception = await Assert.ThrowsAsync<DomainNotFoundException>(() =>
            Handler().Handle(_user.Id, new SubmitAttemptCommand(Guid.CreateVersion7(), _correct.Id, null), Cancel));

        // Assert
        Assert.Equal(TrailErrors.ExerciseNotFound, exception.Code);
        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "Uma conta desconhecida, desabilitada, ou cujo aluno sumiu, é sessão inválida")]
    public async Task Handle_ForAnInvalidAccount_ThrowsInvalidSession()
    {
        // Arrange
        var unknown = Guid.CreateVersion7();
        _users.FindByIdAsync(unknown, Arg.Any<CancellationToken>()).Returns((User?)null);
        var inactive = TestUsers.Inactive();
        _users.FindByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);
        var orphan = User.RegisterStudent("orfa@example.com", TestUsers.Hash, Guid.CreateVersion7());
        _users.FindByIdAsync(orphan.Id, Arg.Any<CancellationToken>()).Returns(orphan);

        // Act & Assert
        foreach (var userId in new[] { unknown, inactive.Id, orphan.Id })
        {
            var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() =>
                Handler().Handle(userId, new SubmitAttemptCommand(_exercise.Id, _correct.Id, null), Cancel));
            Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
        }

        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "RN-RSP-05: o aluno do diagnóstico que erra sem tipo de erro disponível é defeito de dados, e nada é gravado")]
    public async Task Handle_WhenTheErrorTypeIsMissing_ThrowsAndWritesNothing()
    {
        // Arrange
        _errorTypes.FindAsync(_errorType.Id, Arg.Any<CancellationToken>()).Returns((ErrorType?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => AnswerAsync(_wrong));
        AssertNothingWasWritten();
    }

    [Fact(DisplayName = "FA-RSP-10: se outra requisição criou o mesmo cartão antes, o cartão é descartado e a tentativa é gravada")]
    public async Task Handle_WhenTheCardWasCreatedByAnotherRequest_DiscardsTheCardAndKeepsTheAttempt()
    {
        // Arrange: a primeira gravação quebra o índice único do cartão; a segunda passa
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(
            _ => throw new UniqueConstraintViolationException(SubmitAttemptHandler.StudentFlashcardUniqueIndex, new InvalidOperationException()),
            _ => Task.CompletedTask);

        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.Null(result.Flashcard);
        var card = Assert.Single(_addedCards);
        _studentFlashcards.Received(1).Discard(card);
        Assert.Single(_addedAttempts);
        await _unitOfWork.Received(2).SaveChangesAsync(Cancel);
    }

    [Fact(DisplayName = "Uma violação de outro índice único não é engolida")]
    public async Task Handle_WhenAnotherUniqueConstraintIsViolated_Propagates()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ux_student_topic_student_id_topic_id", new InvalidOperationException()));

        // Act & Assert
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => AnswerAsync(_wrong));
        _studentFlashcards.DidNotReceive().Discard(Arg.Any<StudentFlashcard>());
    }

    [Fact(DisplayName = "Acertando, uma violação do índice do cartão não é tratada como corrida (não há cartão)")]
    public async Task Handle_WhenRightAndTheCardIndexIsViolated_Propagates()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException(SubmitAttemptHandler.StudentFlashcardUniqueIndex, new InvalidOperationException()));

        // Act & Assert
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => AnswerAsync(_correct));
    }

    // ---------- O estado da lição na resposta (#91) ----------

    [Fact(DisplayName = "Acertar o único exercício da lição a conclui: a resposta traz a lição concluída, 1 de 1")]
    public async Task Handle_TheRightAnswerOnTheOnlyExercise_CompletesTheLesson()
    {
        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal(new AttemptLessonResult(_exercise.LessonId, LessonStatus.Completed, 1, 1), result.Lesson);
    }

    [Fact(DisplayName = "Errar o exercício deixa a lição em andamento, 0 de 1")]
    public async Task Handle_AWrongAnswer_LeavesTheLessonInProgress()
    {
        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.Equal(new AttemptLessonResult(_exercise.LessonId, LessonStatus.InProgress, 0, 1), result.Lesson);
    }

    [Fact(DisplayName = "O estado considera a resposta que acabou de chegar: o último exercício que faltava conclui a lição, 2 de 2")]
    public async Task Handle_TheLastMissingExercise_CompletesTheLesson()
    {
        // Arrange: a lição tem dois exercícios, e o outro já foi acertado
        var other = Guid.CreateVersion7();
        WithLessonBefore([_exercise.Id, other], attempted: [other], correct: [other]);

        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal((LessonStatus.Completed, 2, 2), (result.Lesson.Status, result.Lesson.CompletedExercises, result.Lesson.TotalExercises));
    }

    [Fact(DisplayName = "Errar um exercício já acertado não desfaz a conclusão da lição")]
    public async Task Handle_AWrongAnswerOnAnExerciseAlreadyRight_KeepsTheLessonCompleted()
    {
        // Arrange
        WithLessonBefore([_exercise.Id], attempted: [_exercise.Id], correct: [_exercise.Id]);

        // Act
        var result = await AnswerAsync(_wrong);

        // Assert
        Assert.Equal((LessonStatus.Completed, 1, 1), (result.Lesson.Status, result.Lesson.CompletedExercises, result.Lesson.TotalExercises));
    }

    [Fact(DisplayName = "Acertar um de dois exercícios deixa a lição em andamento, 1 de 2")]
    public async Task Handle_OneOfTwoExercises_LeavesTheLessonInProgress()
    {
        // Arrange
        WithLessonBefore([_exercise.Id, Guid.CreateVersion7()], attempted: [], correct: []);

        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal((LessonStatus.InProgress, 1, 2), (result.Lesson.Status, result.Lesson.CompletedExercises, result.Lesson.TotalExercises));
    }

    [Fact(DisplayName = "Concluir a lição não domina o tópico: o status do tópico segue o da nota")]
    public async Task Handle_CompletingTheLesson_DoesNotMasterTheTopic()
    {
        // Act
        var result = await AnswerAsync(_correct);

        // Assert
        Assert.Equal(LessonStatus.Completed, result.Lesson.Status);
        Assert.Equal(TopicStatus.Available, result.Topic.Status);
    }
}

