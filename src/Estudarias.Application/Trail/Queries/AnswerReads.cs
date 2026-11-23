using Estudarias.Application.Common;
using Estudarias.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Application.Trail.Queries;

public sealed class AnswerReads(IApplicationDbContext db) : IAnswerReads
{
    /// <summary>
    /// As respostas do aluno num tópico: a janela da nota (as mais recentes) e o total de tentativas no tópico. O total é de todas as
    /// tentativas, e não só da janela (PEND-TRL-01): é ele que o domínio compara com o mínimo de tentativas.
    /// </summary>
    public async Task<(IReadOnlyList<GradedAnswer> Latest, int Total)> OfTopicAsync(
        Guid studentId, Guid topicId, CancellationToken cancellationToken)
    {
        var inTopic = StudentReads.Answers(db, studentId, topicId);

        var total = await inTopic.CountAsync(cancellationToken);
        var latest = await inTopic.Take(TopicScore.Window).ToListAsync(cancellationToken);

        return ([.. latest.Select(answer => new GradedAnswer(answer.Difficulty, answer.IsCorrect))], total);
    }
}
