using System.Runtime.CompilerServices;

namespace Lab1;

/// <summary>
/// Builds numbers straight from the file's bytes without creating strings, and keeps its state between
/// calls so a number split across two buffers is still parsed correctly (used by Task 1a and Task 2).
/// </summary>
public struct NumberParser
{
    private int _value;
    private bool _isNegative;
    private bool _hasDigits;

    /// <summary>Feeds one byte; returns true when it was '\n' and the finished number has been counted.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Feed(byte b, ref SignCounts counts)
    {
        if (b >= '0' && b <= '9')
        {
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
        return false;
    }

    public void FinishLine(ref SignCounts counts)
    {
        if (_hasDigits)
            counts.Add(_isNegative ? -_value : _value);

        _value = 0;
        _isNegative = false;
        _hasDigits = false;
    }

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
