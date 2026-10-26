using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Lab1;

/// <summary>
/// Task 1a: the file is cut into N byte ranges, one thread per range, and a line belongs to the thread
/// whose range contains the line's first byte, so no line is counted twice or skipped.
/// </summary>
public static class Task1a
{
    private const int ChunkSize = 1 << 20;   // each thread reads 1 MB at a time

    public static void Run(CommandLine cli)
    {
        string path = cli.GetString("--file", Paths.Numbers);
        int threadCount = cli.GetInt("--threads", Environment.ProcessorCount);
        Paths.RequireFile(path);

        long fileSize = new FileInfo(path).Length;
        var perThread = new SignCounts[threadCount];

        var stopwatch = Stopwatch.StartNew();

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (start, end) = ThreadRunner.Split(fileSize, t, threadCount);
            perThread[t] = CountRange(path, start, end);
        });

        stopwatch.Stop();

        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;

        Report.Print("task1a", "-", threadCount, stopwatch.Elapsed, total, cli);
    }

    private static SignCounts CountRange(string path, long start, long end)
    {
        var counts = new SignCounts();
        var parser = new NumberParser();
        byte[] buffer = new byte[ChunkSize];

        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Start one byte early: if that byte is not '\n', the first line belongs to the previous thread and is skipped.
        long position = start > 0 ? start - 1 : 0;
        bool skipping = start > 0;

        while (true)
        {
            // After `end` we only need to finish the current line, so small reads are enough.
            long remaining = end - position;
            int toRead = remaining > 0 ? (int)Math.Min(ChunkSize, remaining) : 64;

            int bytesRead = RandomAccess.Read(file, buffer.AsSpan(0, toRead), position);

            if (bytesRead == 0)
            {
                parser.FinishLine(ref counts);
                return counts;
            }

            for (int i = 0; i < bytesRead; i++)
            {
                byte b = buffer[i];
                long bytePosition = position + i;

                if (skipping)
                {
                    if (b == '\n')
                    {
                        skipping = false;
                        if (bytePosition + 1 >= end) return counts;
                    }
                    continue;
                }

                // A line just ended; if the next one starts at or after `end`, it belongs to the next thread.
                if (parser.Feed(b, ref counts) && bytePosition + 1 >= end)
                    return counts;
            }

            position += bytesRead;
        }
    }
}
