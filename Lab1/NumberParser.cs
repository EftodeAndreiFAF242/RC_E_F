using System.Runtime.CompilerServices;

namespace Lab1;

/// <summary>
/// Reads numbers straight from the raw BYTES of the file, one byte at a time,
/// without creating a string for every line (much faster than ReadLine + int.Parse).
///
/// In memory, the text file "-42\n7\n0\n" is just these bytes (one byte per character):
///     '-'  '4'  '2'  '\n'  '7'  '\n'  '0'  '\n'
/// Digits are accumulated into a number; '\n' (new line) means "this number is complete".
///
/// The parser REMEMBERS its state between calls, so a number may be split across two
/// buffers (e.g. "-4" at the end of one read and "2\n" at the start of the next).
/// Used by Task 1a and Task 2, so both parse the numbers in exactly the same way.
/// </summary>
public struct NumberParser
{
    private int _value;        // the digits of the current line, built up so far (without sign)
    private bool _isNegative;  // did we see a '-' on the current line?
    private bool _hasDigits;   // did we see at least one digit on the current line?

    /// <summary>
    /// Feeds one byte to the parser. Returns true when the byte was '\n' (end of a line);
    /// in that case the finished number has already been added to <paramref name="counts"/>.
    /// </summary>
    // `ref SignCounts counts`: we receive the CALLER's variable itself, not a copy,
    // so the counters we increment here are the caller's counters.
    // AggressiveInlining asks the compiler to paste this small method directly into the
    // caller's loop, because it is called ~400 million times (once per byte of the file).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Feed(byte b, ref SignCounts counts)
    {
        if (b >= '0' && b <= '9')
        {
            // Characters are numbers too: '0' = 48, '1' = 49, ... so b - '0' is the digit's value.
            // For "42":  0*10 + 4 = 4,  then  4*10 + 2 = 42.
            _value = _value * 10 + (b - '0');
            _hasDigits = true;
            return false;
        }
        if (b == '-')
        {
            _isNegative = true;
            return false;
        }
        if (b == '\n')
        {
            FinishLine(ref counts);
            return true;
        }
        return false;   // anything else (for example '\r' from Windows line endings) is ignored
    }

    /// <summary>Counts the number collected so far (if any) and resets for the next line.</summary>
    public void FinishLine(ref SignCounts counts)
    {
        if (_hasDigits)
            counts.Add(_isNegative ? -_value : _value);

        _value = 0;
        _isNegative = false;
        _hasDigits = false;
    }

    /// <summary>Counts every number in a buffer that contains whole lines (used by Task 2).</summary>
    // ReadOnlySpan<byte> = a read-only "window" onto a block of memory (for example a byte[]).
    // It lets us pass an array or a part of one without copying it.
    public static SignCounts CountAll(ReadOnlySpan<byte> data)
    {
        var counts = new SignCounts();
        var parser = new NumberParser();

        foreach (byte b in data)
            parser.Feed(b, ref counts);

        parser.FinishLine(ref counts);   // the last line may not end with '\n'
        return counts;
    }
}
