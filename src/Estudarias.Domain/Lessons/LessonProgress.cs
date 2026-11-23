namespace Estudarias.Domain.Lessons;

/// <summary>
/// O progresso de um aluno numa lição: o estado e quantos exercícios ele já acertou, de quantos. É um cálculo puro das tentativas que
/// já são gravadas, sem estado novo: errar depois um exercício já acertado não desfaz a conclusão, e um exercício acrescentado à lição
/// pela carga do conteúdo a faz voltar a <see cref="LessonStatus.InProgress"/> até o aluno o acertar (há conteúdo novo para ver).
/// </summary>
/// <remarks>
/// <b>Concluir a lição não é dominar o tópico.</b> A lição concluída mostra que o aluno passou por todo o conteúdo; quem libera a trilha
/// continua sendo a nota do tópico (<c>TopicMastery</c>). Os dois grupos experimentais veem o mesmo estado, porque ele só depende de
/// acerto, e não do retorno.
/// </remarks>
public readonly record struct LessonProgress(LessonStatus Status, int CompletedExercises, int TotalExercises)
{
    /// <param name="exerciseIds">Os exercícios da lição.</param>
    /// <param name="attemptedExerciseIds">Os exercícios em que o aluno tem pelo menos uma tentativa, certa ou errada.</param>
    /// <param name="correctlyAnsweredExerciseIds">Os exercícios que o aluno acertou pelo menos uma vez.</param>
    /// <remarks>
    /// Ids de exercícios que não são da lição são ignorados, nas duas listas. Um exercício acertado conta como tentado, mesmo que a
    /// lista das tentativas não o traga. Uma lição sem exercícios é <see cref="LessonStatus.NotStarted"/>, com 0 de 0: não conta como
    /// concluída.
    /// </remarks>
    public static LessonProgress Of(
        IReadOnlyCollection<Guid> exerciseIds,
        IReadOnlyCollection<Guid> attemptedExerciseIds,
        IReadOnlyCollection<Guid> correctlyAnsweredExerciseIds)
    {
        ArgumentNullException.ThrowIfNull(exerciseIds);
        ArgumentNullException.ThrowIfNull(attemptedExerciseIds);
        ArgumentNullException.ThrowIfNull(correctlyAnsweredExerciseIds);

        var lesson = exerciseIds.ToHashSet();
        if (lesson.Count == 0)
        {
            return new LessonProgress(LessonStatus.NotStarted, 0, 0);
        }

        var correct = correctlyAnsweredExerciseIds.Where(lesson.Contains).ToHashSet();
        var attempted = attemptedExerciseIds.Where(lesson.Contains).ToHashSet();
        attempted.UnionWith(correct);

        var status = correct.Count == lesson.Count
            ? LessonStatus.Completed
            : attempted.Count == 0 ? LessonStatus.NotStarted : LessonStatus.InProgress;

        return new LessonProgress(status, correct.Count, lesson.Count);
    }
}
