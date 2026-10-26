namespace Lab1;

/// <summary>The three counters every task produces; a struct, so each thread works on its own private copy.</summary>
public struct SignCounts
{
    public long Negative;
    public long Positive;
    public long Zero;

    public readonly long Total => Negative + Positive + Zero;

    public void Add(int value)
    {
        if (value < 0) Negative++;
        else if (value > 0) Positive++;
        else Zero++;
    }

    public static SignCounts operator +(SignCounts a, SignCounts b) => new()
    {
        Negative = a.Negative + b.Negative,
        Positive = a.Positive + b.Positive,
        Zero     = a.Zero     + b.Zero,
    };

    public readonly bool SameAs(SignCounts other) =>
        Negative == other.Negative && Positive == other.Positive && Zero == other.Zero;

    public override readonly string ToString() =>
        $"negative = {Negative:N0}, positive = {Positive:N0}, zero = {Zero:N0}, total = {Total:N0}";
}
