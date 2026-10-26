package lab1;

import java.nio.ByteBuffer;

/** Builds numbers straight from the file's bytes, keeping its state between buffers (same algorithm as NumberParser.cs). */
final class NumberParser {
    private int value;
    private boolean isNegative;
    private boolean hasDigits;

    /** Feeds one byte; returns true when it was '\n' and the finished number has been counted. */
    boolean feed(byte b, SignCounts counts) {
        if (b >= '0' && b <= '9') {
            value = value * 10 + (b - '0');
            hasDigits = true;
            return false;
        }
        if (b == '-') {
            isNegative = true;
            return false;
        }
        if (b == '\n') {
            finishLine(counts);
            return true;
        }
        return false;
    }

    void finishLine(SignCounts counts) {
        if (hasDigits)
            counts.add(isNegative ? -value : value);
        value = 0;
        isNegative = false;
        hasDigits = false;
    }

    static SignCounts countAll(ByteBuffer data, int length) {
        SignCounts counts = new SignCounts();
        NumberParser parser = new NumberParser();
        for (int i = 0; i < length; i++)
            parser.feed(data.get(i), counts);
        parser.finishLine(counts);   // the last line may not end with '\n'
        return counts;
    }
}
