package lab1;

import java.nio.ByteBuffer;

/**
 * Reads numbers straight from the raw BYTES of the file, without creating a String per line.
 * Exactly the same algorithm as NumberParser.cs (see the explanation there):
 *   digits are accumulated into a number, '-' marks it negative, '\n' finishes the line.
 * The parser remembers its state between calls, so a number may be split across two buffers.
 *
 * Java detail: `byte` is SIGNED in Java (-128..127), in C# it is unsigned (0..255).
 * The characters we care about ('0'-'9', '-', '\n') are all below 128, so it makes no difference here.
 */
final class NumberParser {
    private int value;           // the digits of the current line, built up so far (without sign)
    private boolean isNegative;  // did we see a '-' on the current line?
    private boolean hasDigits;   // did we see at least one digit on the current line?

    /** Feeds one byte. Returns true when the byte was '\n' (the finished number is already counted). */
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
        return false;   // anything else (for example '\r') is ignored
    }

    /** Counts the number collected so far (if any) and resets for the next line. */
    void finishLine(SignCounts counts) {
        if (hasDigits)
            counts.add(isNegative ? -value : value);
        value = 0;
        isNegative = false;
        hasDigits = false;
    }

    /** Counts every number in the first `length` bytes of a buffer that holds whole lines (Task 2). */
    static SignCounts countAll(ByteBuffer data, int length) {
        SignCounts counts = new SignCounts();
        NumberParser parser = new NumberParser();
        for (int i = 0; i < length; i++)
            parser.feed(data.get(i), counts);   // get(i) = "absolute" read of byte i
        parser.finishLine(counts);              // the last line may not end with '\n'
        return counts;
    }
}
