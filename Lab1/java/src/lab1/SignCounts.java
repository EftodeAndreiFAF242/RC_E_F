package lab1;

import java.util.Locale;

/**
 * The result every task must produce: how many numbers are negative, positive and zero.
 *
 * C# vs Java: in C# this is a `struct` (a value type: assigning it COPIES it). Java has no
 * user-defined value types yet, so this is a normal class: a variable holds a REFERENCE to
 * the object. Each thread therefore creates its own `new SignCounts()` to get a private copy.
 */
final class SignCounts {
    long negative;
    long positive;
    long zero;

    long total() { return negative + positive + zero; }

    /** Looks at one number and increments the matching counter. */
    void add(int value) {
        if (value < 0) negative++;
        else if (value > 0) positive++;
        else zero++;
    }

    /** Adds another result to this one. (Java has no operator overloading, so no `a + b`.) */
    void addAll(SignCounts other) {
        negative += other.negative;
        positive += other.positive;
        zero     += other.zero;
    }

    boolean sameAs(SignCounts other) {
        return negative == other.negative && positive == other.positive && zero == other.zero;
    }

    // %,d = a whole number with thousands separators. Locale.ROOT makes the separator ','
    // whatever the Windows language settings are (the C# version uses InvariantGlobalization).
    @Override
    public String toString() {
        return String.format(Locale.ROOT, "negative = %,d, positive = %,d, zero = %,d, total = %,d",
                             negative, positive, zero, total());
    }
}
