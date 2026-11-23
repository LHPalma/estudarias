using Estudarias.Application.Common;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// As consultas do aluno que as leituras da trilha e do tópico dividem: quem é o aluno da conta, o status de cada tópico para ele e as
/// respostas que compõem a nota. Ficam num lugar só para os dois endpoints calcularem o mesmo status e a mesma nota.
/// </summary>
internal static class StudentReads
{
    /// <summary>Uma resposta já corrigida, com o tópico do exercício (o da lição dele): o que a nota usa, e nada além disso.</summary>
    internal sealed record AnswerRow(Guid TopicId, Difficulty Difficulty, bool IsCorrect);

    /// <summary>O aluno ligado à conta, ou nulo quando a conta não existe, está desabilitada ou não tem aluno.</summary>
    public static async Task<Guid?> StudentIdOfAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.Users
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.StudentId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// O status dos tópicos que o aluno liberou. O status não é coluna: deriva de <c>mastered_at</c>, e um tópico sem linha de progresso
    /// está bloqueado (use <see cref="StatusOf"/>).
    /// </summary>
    public static async Task<Dictionary<Guid, TopicStatus>> StatusesAsync(
        IApplicationDbContext db, Guid studentId, CancellationToken cancellationToken) =>
        (await db.Students
            .Where(student => student.Id == studentId)
            .SelectMany(student => student.Progress)
            .Select(progress => new { progress.TopicId, progress.MasteredAt })
            .ToListAsync(cancellationToken))
        .ToDictionary(
            progress => progress.TopicId,
            progress => progress.MasteredAt is null ? TopicStatus.Available : TopicStatus.Mastered);

    public static TopicStatus StatusOf(Dictionary<Guid, TopicStatus> statuses, Guid topicId) =>
        statuses.TryGetValue(topicId, out var status) ? status : TopicStatus.Locked;

    /// <summary>
    /// As respostas do aluno (de um tópico só, quando <paramref name="topicId"/> vem), da mais recente para a mais antiga, com o
    /// desempate pelo id (v7, que cresce com o tempo) para duas respostas no mesmo instante saírem sempre na mesma ordem. Só as colunas
    /// que a nota usa; o tópico de um exercício é o da lição dele.
    /// </summary>
    internal static IQueryable<AnswerRow> Answers(IApplicationDbContext db, Guid studentId, Guid? topicId = null)
    {
        var sources =
            from attempt in db.Attempts
            join exercise in db.Exercises on attempt.ExerciseId equals exercise.Id
            join lesson in db.Lessons on exercise.LessonId equals lesson.Id
            where attempt.StudentId == studentId
            select new { attempt, exercise, lesson };

        // O filtro entra antes da projeção: o EF Core não traduz um Where sobre o resultado já projetado num tipo nosso.
        if (topicId is { } id)
        {
            sources = sources.Where(source => source.lesson.TopicId == id);
        }

        return sources
            .OrderByDescending(source => source.attempt.AnsweredAt)
            .ThenByDescending(source => source.attempt.Id)
            .Select(source => new AnswerRow(
                source.lesson.TopicId,
                source.exercise.Difficulty,
                source.exercise.Alternatives.Any(
                    alternative => alternative.Id == source.attempt.AlternativeId && alternative.IsCorrect)));
    }

    /// <summary>Para cada tópico com respostas, as da janela da nota (as mais recentes), da mais recente para a mais antiga.</summary>
    public static async Task<Dictionary<Guid, IReadOnlyList<GradedAnswer>>> LatestAnswersByTopicAsync(
        IApplicationDbContext db, Guid studentId, CancellationToken cancellationToken) =>
        (await Answers(db, studentId).ToListAsync(cancellationToken))
        .GroupBy(answer => answer.TopicId)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<GradedAnswer>)[.. group
                .Take(TopicScore.Window)
                .Select(answer => new GradedAnswer(answer.Difficulty, answer.IsCorrect))]);

    /// <summary>O progresso de uma lição para o aluno, com o tópico dela.</summary>
    internal sealed record LessonProgressRow(Guid LessonId, Guid TopicId, LessonProgress Progress);

    /// <summary>
    /// Os exercícios em que o aluno tem tentativa e os que ele acertou pelo menos uma vez (de todos os exercícios, ou só dos de
    /// <paramref name="exerciseIds"/>). É o que <see cref="LessonProgress.Of"/> precisa das tentativas: ids distintos, calculados no
    /// banco, em vez de trazer uma linha por tentativa.
    /// </summary>
    internal static async Task<(IReadOnlyCollection<Guid> Attempted, IReadOnlyCollection<Guid> Correct)> AttemptedAndCorrectAsync(
        IApplicationDbContext db, Guid studentId, IReadOnlyCollection<Guid>? exerciseIds, CancellationToken cancellationToken)
    {
        var attempts = db.Attempts.Where(attempt => attempt.StudentId == studentId);
        if (exerciseIds is not null)
        {
            attempts = attempts.Where(attempt => exerciseIds.Contains(attempt.ExerciseId));
        }

        var attempted = await attempts.Select(attempt => attempt.ExerciseId).Distinct().ToListAsync(cancellationToken);

        var correct = await (
            from attempt in attempts
            join exercise in db.Exercises on attempt.ExerciseId equals exercise.Id
            where exercise.Alternatives.Any(alternative => alternative.Id == attempt.AlternativeId && alternative.IsCorrect)
            select attempt.ExerciseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return (attempted, correct);
    }

    /// <summary>
    /// O progresso de cada lição para o aluno (as de um tópico só, quando <paramref name="topicId"/> vem). Inclui a lição sem exercícios
    /// (não iniciada, 0 de 0). O estado é derivado das tentativas já gravadas, sem estado novo (RN-TRL-13).
    /// </summary>
    internal static async Task<IReadOnlyList<LessonProgressRow>> LessonProgressAsync(
        IApplicationDbContext db, Guid studentId, Guid? topicId, CancellationToken cancellationToken)
    {
        var lessonsQuery = db.Lessons.AsQueryable();
        if (topicId is { } id)
        {
            lessonsQuery = lessonsQuery.Where(lesson => lesson.TopicId == id);
        }

        var lessons = await lessonsQuery.Select(lesson => new { lesson.Id, lesson.TopicId }).ToListAsync(cancellationToken);
        var lessonIds = lessons.Select(lesson => lesson.Id).ToList();

        var exercises = (await db.Exercises
                .Where(exercise => lessonIds.Contains(exercise.LessonId))
                .Select(exercise => new { exercise.Id, exercise.LessonId })
                .ToListAsync(cancellationToken))
            .ToLookup(exercise => exercise.LessonId, exercise => exercise.Id);

        var (attempted, correct) = await AttemptedAndCorrectAsync(
            db, studentId, [.. exercises.SelectMany(group => group)], cancellationToken);

        return [.. lessons.Select(lesson => new LessonProgressRow(
            lesson.Id,
            lesson.TopicId,
            LessonProgress.Of([.. exercises[lesson.Id]], attempted, correct)))];
    }
}
