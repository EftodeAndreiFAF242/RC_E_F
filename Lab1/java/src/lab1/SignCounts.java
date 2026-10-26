package lab1;

import java.util.Locale;

/** The three counters; Java has no structs, so every thread creates its own SignCounts object. */
final class SignCounts {
    long negative;
    long positive;
    long zero;

    long total() { return negative + positive + zero; }

    void add(int value) {
        if (value < 0) negative++;
        else if (value > 0) positive++;
        else zero++;
    }

    void addAll(SignCounts other) {
        negative += other.negative;
        positive += other.positive;
        zero     += other.zero;
    }

    boolean sameAs(SignCounts other) {
        return negative == other.negative && positive == other.positive && zero == other.zero;
    }

    @Override
    public String toString() {
        return String.format(Locale.ROOT, "negative = %,d, positive = %,d, zero = %,d, total = %,d",
                             negative, positive, zero, total());
    }
}
