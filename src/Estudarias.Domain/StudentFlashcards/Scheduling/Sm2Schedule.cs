namespace Estudarias.Domain.StudentFlashcards.Scheduling;

/// <summary>O estado de um cartão no SM-2 original (Wozniak) e o cálculo da revisão seguinte.</summary>
public readonly record struct Sm2Schedule(int Repetitions, int IntervalDays, decimal EaseFactor)
{
    public const decimal InitialEaseFactor = 2.5m;
    public const decimal MinEaseFactor = 1.3m;
    public const int MinQuality = 0;
    public const int MaxQuality = 5;
    public const int PassingQuality = 3;

    public static Sm2Schedule Initial { get; } = new(0, 1, InitialEaseFactor);

    /// <summary>Aplica uma revisão com a nota <paramref name="quality"/>, de 0 a 5.</summary>
    public Sm2Schedule Next(int quality)
    {
        if (quality is < MinQuality or > MaxQuality)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quality), quality, $"SM-2 quality must be between {MinQuality} and {MaxQuality}.");
        }

        if (quality < PassingQuality)
        {
            return this with { Repetitions = 0, IntervalDays = 1 };
        }

        var interval = Repetitions switch
        {
            0 => 1,
            1 => 6,
            _ => (int)Math.Round(IntervalDays * EaseFactor, MidpointRounding.AwayFromZero),
        };

        var miss = MaxQuality - quality;
        var easeFactor = Math.Round(EaseFactor + (0.1m - miss * (0.08m + miss * 0.02m)), 2, MidpointRounding.AwayFromZero);

        return new Sm2Schedule(Repetitions + 1, interval, Math.Max(MinEaseFactor, easeFactor));
    }
}
