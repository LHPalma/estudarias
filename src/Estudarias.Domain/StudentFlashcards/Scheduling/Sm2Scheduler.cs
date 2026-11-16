using System.Text.Json;

namespace Estudarias.Domain.StudentFlashcards.Scheduling;

/// <summary>
/// O adapter do SM-2 para o port <see cref="IReviewScheduler"/>: traduz a nota neutra para a escala de 0 a 5 e guarda o
/// <see cref="Sm2Schedule"/> como JSON, ex.: <c>{"repetitions":0,"intervalDays":1,"easeFactor":2.5}</c>.
/// </summary>
public sealed class Sm2Scheduler : IReviewScheduler
{
    public const string AlgorithmName = "sm2";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Algorithm => AlgorithmName;

    public ReviewOutcome Start(DateTimeOffset now)
    {
        var schedule = Sm2Schedule.Initial;
        return new ReviewOutcome(WriteState(schedule), now.AddDays(schedule.IntervalDays));
    }

    public ReviewOutcome Review(string state, ReviewRating rating, DateTimeOffset reviewedAt)
    {
        var next = ReadState(state).Next(QualityOf(rating));
        return new ReviewOutcome(WriteState(next), reviewedAt.AddDays(next.IntervalDays));
    }

    /// <summary>Converte a nota neutra para a nota do SM-2: Again 1, Hard 3, Good 4, Easy 5.</summary>
    public static int QualityOf(ReviewRating rating) => rating switch
    {
        ReviewRating.Again => 1,
        ReviewRating.Hard => 3,
        ReviewRating.Good => 4,
        ReviewRating.Easy => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Review rating is not valid."),
    };

    public static string WriteState(Sm2Schedule schedule) =>
        JsonSerializer.Serialize(
            new StoredState
            {
                Repetitions = schedule.Repetitions,
                IntervalDays = schedule.IntervalDays,
                EaseFactor = schedule.EaseFactor,
            },
            JsonOptions);

    /// <summary>Lê o estado gravado. Um estado ilegível é dado corrompido e lança <see cref="InvalidOperationException"/>.</summary>
    public static Sm2Schedule ReadState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new InvalidOperationException("SM-2 scheduling state is empty.");
        }

        StoredState? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredState>(state, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("SM-2 scheduling state is not readable.", exception);
        }

        if (stored is null
            || stored.Repetitions < 0
            || stored.IntervalDays < 1
            || stored.EaseFactor < Sm2Schedule.MinEaseFactor)
        {
            throw new InvalidOperationException("SM-2 scheduling state has invalid values.");
        }

        return new Sm2Schedule(stored.Repetitions, stored.IntervalDays, stored.EaseFactor);
    }

    private sealed class StoredState
    {
        public required int Repetitions { get; init; }
        public required int IntervalDays { get; init; }
        public required decimal EaseFactor { get; init; }
    }
}
