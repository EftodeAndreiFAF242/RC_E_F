namespace Lab1;

/// <summary>
/// The result every task must produce: how many numbers are negative, positive and zero.
/// </summary>
// `struct` = a VALUE type: assigning it copies all its fields (like an int).
// A `class` is a REFERENCE type: assigning it copies only a pointer, so two variables
// can point to the same object. For a small per-thread result a struct is ideal:
// every thread gets its own private copy automatically.
public struct SignCounts
{
    // `long` = 64-bit integer. `int` (32-bit, max ~2.1 billion) would fit 50 million too,
    // but long costs nothing here and never overflows for bigger experiments.
    public long Negative;
    public long Positive;
    public long Zero;

    // `=>` defines a property computed on demand ("expression-bodied member").
    // `readonly` promises this member does not modify the struct.
    public readonly long Total => Negative + Positive + Zero;

    /// <summary>Looks at one number and increments the matching counter.</summary>
    public void Add(int value)
    {
        if (value < 0) Negative++;
        else if (value > 0) Positive++;
        else Zero++;
    }

    // Operator overloading: lets us write `total = a + b` for two SignCounts values.
    public static SignCounts operator +(SignCounts a, SignCounts b) => new()
    {
        Negative = a.Negative + b.Negative,
        Positive = a.Positive + b.Positive,
        Zero     = a.Zero     + b.Zero,
    };

    public readonly bool SameAs(SignCounts other) =>
        Negative == other.Negative && Positive == other.Positive && Zero == other.Zero;

    // $"..." is an interpolated string: {expression} is replaced by its value.
    // ":N0" formats a number with thousands separators and 0 decimals: 25,000,123
    public override readonly string ToString() =>
        $"negative = {Negative:N0}, positive = {Positive:N0}, zero = {Zero:N0}, total = {Total:N0}";
}
