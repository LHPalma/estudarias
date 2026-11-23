using Estudarias.Domain.StudentFlashcards.Scheduling;

namespace Estudarias.Application.Reviews;

/// <summary>Os agendadores registrados, um por algoritmo. Dois com o mesmo algoritmo é erro de configuração, achado na primeira chamada.</summary>
public sealed class ReviewSchedulers(IEnumerable<IReviewScheduler> schedulers) : IReviewSchedulers
{
    private readonly IReadOnlyDictionary<string, IReviewScheduler> _byAlgorithm = schedulers
        .GroupBy(scheduler => scheduler.Algorithm, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Count() == 1
                ? group.Single()
                : throw new InvalidOperationException($"More than one review scheduler is registered for the algorithm '{group.Key}'."),
            StringComparer.Ordinal);

    public IReviewScheduler For(string algorithm) =>
        _byAlgorithm.TryGetValue(algorithm, out var scheduler)
            ? scheduler
            : throw new InvalidOperationException($"No review scheduler is registered for the algorithm '{algorithm}'.");
}
