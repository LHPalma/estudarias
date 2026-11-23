using Estudarias.Domain.Lessons;

namespace Estudarias.Domain.UnitTests.Lessons;

public class LessonProgressTests
{
    private static readonly Guid First = Guid.CreateVersion7();
    private static readonly Guid Second = Guid.CreateVersion7();
    private static readonly Guid Third = Guid.CreateVersion7();
    private static readonly Guid FromAnotherLesson = Guid.CreateVersion7();

    private static Guid[] Lesson => [First, Second, Third];

    [Fact(DisplayName = "Sem nenhuma tentativa, a lição não foi iniciada: 0 de 3")]
    public void Of_WithoutAttempts_IsNotStarted()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [], []);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.NotStarted, 0, 3), progress);
    }

    [Fact(DisplayName = "Com tentativas só erradas, a lição está em andamento: 0 de 3")]
    public void Of_WithOnlyWrongAttempts_IsInProgress()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [First, Second], []);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.InProgress, 0, 3), progress);
    }

    [Fact(DisplayName = "Com parte dos exercícios acertada, a lição está em andamento: 2 de 3")]
    public void Of_WithPartOfTheExercisesAnsweredRight_IsInProgress()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [First, Second, Third], [First, Second]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.InProgress, 2, 3), progress);
    }

    [Fact(DisplayName = "Com todos os exercícios acertados pelo menos uma vez, a lição está concluída: 3 de 3")]
    public void Of_WithEveryExerciseAnsweredRight_IsCompleted()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [First, Second, Third], [First, Second, Third]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.Completed, 3, 3), progress);
    }

    [Fact(DisplayName = "Errar depois um exercício já acertado não desfaz a conclusão: o exercício continua na lista dos acertados")]
    public void Of_AWrongAttemptAfterARightOne_DoesNotUndoTheCompletion()
    {
        // Arrange: o primeiro exercício foi acertado e depois errado; as duas coisas aparecem nas tentativas, e a lista dos acertados o mantém
        var progress = LessonProgress.Of(Lesson, [First, First, Second, Third], [First, Second, Third]);

        // Assert
        Assert.Equal(LessonStatus.Completed, progress.Status);
    }

    [Fact(DisplayName = "Ids de exercícios que não são da lição são ignorados, nas duas listas")]
    public void Of_IgnoresTheExercisesOfAnotherLesson()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [FromAnotherLesson], [FromAnotherLesson]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.NotStarted, 0, 3), progress);
    }

    [Fact(DisplayName = "Um exercício de outra lição acertado não completa a lição nem entra na contagem")]
    public void Of_ARightAnswerFromAnotherLesson_DoesNotCount()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [First, Second, FromAnotherLesson], [First, Second, FromAnotherLesson]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.InProgress, 2, 3), progress);
    }

    [Fact(DisplayName = "Uma lição sem exercícios não foi iniciada, com 0 de 0, e nunca está concluída")]
    public void Of_ALessonWithoutExercises_IsNotStartedAndNeverCompleted()
    {
        // Act
        var progress = LessonProgress.Of([], [First], [First]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.NotStarted, 0, 0), progress);
    }

    [Fact(DisplayName = "Um exercício acertado conta como tentado, mesmo que a lista das tentativas não o traga")]
    public void Of_ARightExercise_CountsAsAttempted()
    {
        // Act
        var progress = LessonProgress.Of(Lesson, [], [First]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.InProgress, 1, 3), progress);
    }

    [Fact(DisplayName = "Ids repetidos nas listas não contam duas vezes, nem na lição")]
    public void Of_CountsEachExerciseOnce()
    {
        // Act
        var progress = LessonProgress.Of([First, First, Second], [First, First], [First, First]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.InProgress, 1, 2), progress);
    }

    [Fact(DisplayName = "Um exercício acrescentado a uma lição concluída a faz voltar a em andamento")]
    public void Of_ANewExerciseOnACompletedLesson_BringsItBackToInProgress()
    {
        // Arrange
        var before = LessonProgress.Of([First, Second], [First, Second], [First, Second]);

        // Act: a carga do conteúdo acrescentou o terceiro exercício, que o aluno nunca respondeu
        var after = LessonProgress.Of([First, Second, Third], [First, Second], [First, Second]);

        // Assert
        Assert.Equal(LessonStatus.Completed, before.Status);
        Assert.Equal(new LessonProgress(LessonStatus.InProgress, 2, 3), after);
    }

    [Fact(DisplayName = "A conclusão não depende da ordem das listas")]
    public void Of_DoesNotDependOnTheOrder()
    {
        // Act
        var progress = LessonProgress.Of([Third, First, Second], [Second, Third, First], [Third, Second, First]);

        // Assert
        Assert.Equal(new LessonProgress(LessonStatus.Completed, 3, 3), progress);
    }

    [Fact(DisplayName = "Uma lista nula lança ArgumentNullException")]
    public void Of_WithANullList_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => LessonProgress.Of(null!, [], []));
        Assert.Throws<ArgumentNullException>(() => LessonProgress.Of(Lesson, null!, []));
        Assert.Throws<ArgumentNullException>(() => LessonProgress.Of(Lesson, [], null!));
    }
}
