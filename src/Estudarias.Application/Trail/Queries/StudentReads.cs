using Estudarias.Application.Common;
using Estudarias.Domain.Exercises;
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
    private sealed record AnswerRow(Guid TopicId, Difficulty Difficulty, bool IsCorrect);

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
    /// As respostas do aluno nos exercícios, da mais recente para a mais antiga, com o desempate pelo id (v7, que cresce com o tempo)
    /// para duas respostas no mesmo instante saírem sempre na mesma ordem. Só as colunas que a nota usa.
    /// </summary>
    private static IQueryable<AnswerRow> Answers(IApplicationDbContext db, Guid studentId) =>
        from attempt in db.Attempts
        join exercise in db.Exercises on attempt.ExerciseId equals exercise.Id
        join lesson in db.Lessons on exercise.LessonId equals lesson.Id
        where attempt.StudentId == studentId
        orderby attempt.AnsweredAt descending, attempt.Id descending
        select new AnswerRow(
            lesson.TopicId,
            exercise.Difficulty,
            exercise.Alternatives.Any(alternative => alternative.Id == attempt.AlternativeId && alternative.IsCorrect));

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

    /// <summary>
    /// As respostas do aluno num tópico: a janela da nota (as mais recentes) e o total de tentativas no tópico. O total é de todas as
    /// tentativas, e não só da janela (PEND-TRL-01): é ele que o domínio compara com o mínimo de tentativas.
    /// </summary>
    public static async Task<(IReadOnlyList<GradedAnswer> Latest, int Total)> AnswersOfTopicAsync(
        IApplicationDbContext db, Guid studentId, Guid topicId, CancellationToken cancellationToken)
    {
        var inTopic = Answers(db, studentId).Where(answer => answer.TopicId == topicId);

        var total = await inTopic.CountAsync(cancellationToken);
        var latest = await inTopic.Take(TopicScore.Window).ToListAsync(cancellationToken);

        return ([.. latest.Select(answer => new GradedAnswer(answer.Difficulty, answer.IsCorrect))], total);
    }
}
