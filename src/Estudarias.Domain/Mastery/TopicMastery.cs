using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;

namespace Estudarias.Domain.Mastery;

/// <summary>
/// Decide quando um tópico está dominado pelo aluno e quais tópicos dependentes passam a ficar disponíveis. Opera sobre
/// o progresso do <see cref="Student"/> e os pré-requisitos dos <see cref="Topic"/>; nada volta atrás.
/// </summary>
public static class TopicMastery
{
    public const decimal Threshold = 70m;
    public const int MinAttempts = 5;

    /// <summary>Dominado: nota de pelo menos 70 e pelo menos 5 tentativas no tópico.</summary>
    public static bool IsMastered(TopicScore score, int attemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);

        return score.Value >= Threshold && attemptCount >= MinAttempts;
    }

    /// <summary>Libera os tópicos sem pré-requisito. Idempotente: devolve só os liberados nesta chamada.</summary>
    public static IReadOnlyCollection<Guid> UnlockInitial(
        Student student, IReadOnlyCollection<Topic> topics, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(clock);

        var unlocked = new List<Guid>();
        foreach (var topic in topics.Where(topic => topic.Prerequisites.Count == 0))
        {
            if (student.StatusOf(topic.Id) == TopicStatus.Locked)
            {
                student.Unlock(topic.Id, clock);
                unlocked.Add(topic.Id);
            }
        }

        return unlocked;
    }

    /// <summary>
    /// Marca o tópico como dominado, se a nota e as tentativas bastarem, e libera os dependentes cujos pré-requisitos
    /// estão todos dominados. Devolve só os tópicos liberados nesta chamada.
    /// </summary>
    public static IReadOnlyCollection<Guid> Evaluate(
        Student student,
        Guid topicId,
        TopicScore score,
        int attemptCount,
        IReadOnlyCollection<Topic> topics,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);

        if (student.StatusOf(topicId) == TopicStatus.Locked)
        {
            throw new DomainValidationException(
                StudentErrors.TopicLocked, "A locked topic cannot be practiced or mastered.");
        }

        if (IsMastered(score, attemptCount))
        {
            student.MarkMastered(topicId, clock);
        }

        if (student.StatusOf(topicId) != TopicStatus.Mastered)
        {
            return [];
        }

        var unlocked = new List<Guid>();
        foreach (var dependent in topics.Where(topic => topic.Prerequisites.Contains(topicId)))
        {
            var ready = student.StatusOf(dependent.Id) == TopicStatus.Locked
                && dependent.Prerequisites.All(requisite => student.StatusOf(requisite) == TopicStatus.Mastered);

            if (ready)
            {
                student.Unlock(dependent.Id, clock);
                unlocked.Add(dependent.Id);
            }
        }

        return unlocked;
    }
}
