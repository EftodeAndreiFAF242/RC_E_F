using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Lab1;

/// <summary>
/// Task 1a: counting with threads.
///
/// The file is cut into N equal BYTE ranges, one per thread. Each thread opens the file
/// itself, reads ONLY its range (plus the few bytes needed to finish its last line), and
/// parses the bytes with NumberParser.
///
/// THE BOUNDARY PROBLEM: a cut at byte position `start` usually lands in the middle of a
/// line, e.g. "...\n-12|3456\n..." ('|' = cut). Who counts "-123456"?
///
/// RULE: a line belongs to the thread whose range [start, end) contains the line's FIRST byte.
///   - At the start: if the byte just before `start` is '\n', a line begins exactly at `start`
///     and it's ours. Otherwise the line began earlier, belongs to the previous thread, and
///     we skip forward past the next '\n'.
///   - At the end: we keep going until we finish a line whose '\n' is at position >= end-1,
///     i.e. the next line would start at or after `end` and so belongs to the next thread.
/// Every line has exactly one first byte, and the ranges don't overlap and cover the whole
/// file, so every line is counted exactly once: nothing twice, nothing skipped.
/// </summary>
public static class Task1a
{
    private const int ChunkSize = 1 << 20;   // each thread reads 1 MB at a time

    public static void Run(CommandLine cli)
    {
        string path = cli.GetString("--file", Paths.Numbers);
        int threadCount = cli.GetInt("--threads", Environment.ProcessorCount);
        Paths.RequireFile(path);

        long fileSize = new FileInfo(path).Length;          // size in bytes (~395 MB)
        var perThread = new SignCounts[threadCount];        // one result slot per thread

        // Stopwatch = a high-precision timer. We measure from creating the threads until
        // the last one has finished.
        var stopwatch = Stopwatch.StartNew();

        ThreadRunner.RunAll(threadCount, t =>
        {
            // `var (start, end) = ...` "deconstructs" the tuple into two variables.
            var (start, end) = ThreadRunner.Split(fileSize, t, threadCount);
            perThread[t] = CountRange(path, start, end);    // each thread writes only its own slot
        });

        stopwatch.Stop();

        // Add the per-thread results together (done by the main thread, after all threads finished).
        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;

        Report.Print("task1a", "-", threadCount, stopwatch.Elapsed, total, cli);
    }

    /// <summary>Counts the numbers on every line whose first byte lies in [start, end).</summary>
    private static SignCounts CountRange(string path, long start, long end)
    {
        var counts = new SignCounts();
        var parser = new NumberParser();
        byte[] buffer = new byte[ChunkSize];   // this thread's private buffer

        // Every thread opens its OWN handle to the file. `using` closes it when we return.
        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // If we don't start at 0, begin one byte early so we can see the byte before `start`.
        long position = start > 0 ? start - 1 : 0;
        bool skipping = start > 0;   // true while we are skipping the previous thread's line

        while (true)
        {
            // Read up to the end of our range. Once we are past `end` we only need to finish
            // the current line (at most ~9 bytes), so a small read of 64 bytes is enough.
            long remaining = end - position;
            int toRead = remaining > 0 ? (int)Math.Min(ChunkSize, remaining) : 64;

            // RandomAccess.Read reads from an EXPLICIT file offset: no shared "current position",
            // so it's obvious that each thread reads its own part of the file.
            int bytesRead = RandomAccess.Read(file, buffer.AsSpan(0, toRead), position);

            if (bytesRead == 0)                       // 0 bytes = end of the file
            {
                parser.FinishLine(ref counts);        // the last line may have no '\n'
                return counts;
            }

            for (int i = 0; i < bytesRead; i++)
            {
                byte b = buffer[i];
                long bytePosition = position + i;     // where this byte is in the whole file

                if (skipping)
                {
                    // Skip bytes until the '\n' that ends the line we don't own.
                    if (b == '\n')
                    {
                        skipping = false;
                        if (bytePosition + 1 >= end) return counts;   // no line starts in our range at all
                    }
                    continue;   // go to the next byte
                }

                // Feed returns true when b was '\n', i.e. a line was just completed.
                // If the NEXT line would start at or after `end`, it belongs to the next thread: stop.
                if (parser.Feed(b, ref counts) && bytePosition + 1 >= end)
                    return counts;
            }

            position += bytesRead;
        }
    }
}
