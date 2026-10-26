using System.Diagnostics;

namespace Lab1;

/// <summary>
/// Task 1b: counts an int[] with one thread per logical processor in three ways - shared counters without
/// synchronisation, shared counters behind a lock, and a private counter per thread added up at the end.
/// </summary>
public static class Task1b
{
    // One object shared by all threads in versions 1 and 2.
    private sealed class SharedCounters
    {
        public long Negative;
        public long Positive;
        public long Zero;

        public SignCounts ToSignCounts() => new() { Negative = Negative, Positive = Positive, Zero = Zero };
    }

    private static readonly Lock Gate = new();

    public static void Run(CommandLine cli)
    {
        string path = cli.GetString("--file", Paths.Numbers);
        int threadCount = cli.GetInt("--threads", Environment.ProcessorCount);
        string version = cli.GetString("--version", "all");
        Paths.RequireFile(path);

        Console.WriteLine($"Loading all numbers into an int array (not measured)...");
        int[] numbers = LoadNumbers(path);

        // Collect the loading garbage now, so the GC does not run in the middle of a measurement.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        Console.WriteLine($"Counting {numbers.Length:N0} numbers with {threadCount} threads.");

        switch (version)
        {
            case "1": Measure("v1-no-sync", () => CountUnsynchronized(numbers, threadCount), threadCount, cli); break;
            case "2": Measure("v2-lock",    () => CountWithLock(numbers, threadCount),       threadCount, cli); break;
            case "3": Measure("v3-local",   () => CountPerThread(numbers, threadCount),      threadCount, cli); break;
            case "all":
                for (int run = 1; run <= 3; run++)
                    Measure("v1-no-sync", () => CountUnsynchronized(numbers, threadCount), threadCount, cli);
                Measure("v2-lock",  () => CountWithLock(numbers, threadCount),  threadCount, cli);
                Measure("v3-local", () => CountPerThread(numbers, threadCount), threadCount, cli);
                break;
            default:
                throw new ArgumentException("--version must be 1, 2, 3 or all");
        }
    }

    private static void Measure(string name, Func<SignCounts> countingMethod, int threads, CommandLine cli)
    {
        var stopwatch = Stopwatch.StartNew();
        SignCounts result = countingMethod();
        stopwatch.Stop();
        Report.Print("task1b", name, threads, stopwatch.Elapsed, result, cli);
    }

    // Version 1, a race on purpose: counter++ is read-add-write, so two threads can write the same value and lose a count.
    private static SignCounts CountUnsynchronized(int[] numbers, int threadCount)
    {
        var shared = new SharedCounters();

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(numbers.Length, t, threadCount);
            for (long i = from; i < to; i++)
            {
                int value = numbers[i];
                if (value < 0) shared.Negative++;
                else if (value > 0) shared.Positive++;
                else shared.Zero++;
            }
        });

        return shared.ToSignCounts();
    }

    // Version 2: correct, but all threads fight for the same lock 50 million times.
    private static SignCounts CountWithLock(int[] numbers, int threadCount)
    {
        var shared = new SharedCounters();

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(numbers.Length, t, threadCount);
            for (long i = from; i < to; i++)
            {
                int value = numbers[i];
                lock (Gate)
                {
                    if (value < 0) shared.Negative++;
                    else if (value > 0) shared.Positive++;
                    else shared.Zero++;
                }
            }
        });

        return shared.ToSignCounts();
    }

    // Version 3: every thread counts into its own local variables, and the results are added once after Join.
    private static SignCounts CountPerThread(int[] numbers, int threadCount)
    {
        var perThread = new SignCounts[threadCount];

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(numbers.Length, t, threadCount);

            var local = new SignCounts();
            for (long i = from; i < to; i++)
                local.Add(numbers[i]);

            perThread[t] = local;
        });

        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;
        return total;
    }

    private static int[] LoadNumbers(string path)
    {
        var list = new List<int>(capacity: 50_000_000);

        foreach (string line in File.ReadLines(path))
            list.Add(int.Parse(line));

        return list.ToArray();
    }
}
