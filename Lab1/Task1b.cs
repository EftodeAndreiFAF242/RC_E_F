using System.Diagnostics;

namespace Lab1;

/// <summary>
/// Task 1b: three ways to count. All numbers are first loaded into an int[] (not measured),
/// then the array is split into equal pieces, one per thread, and counted in three ways:
///   1. shared counters without synchronisation  -> fast-ish but WRONG (lost updates)
///   2. shared counters protected by a lock      -> correct but very slow
///   3. a private counter per thread, summed at the end -> correct AND fast
/// </summary>
public static class Task1b
{
    /// <summary>
    /// Version 1 and 2 share ONE object of this class between all threads.
    /// A class is a reference type: every thread gets a reference (a pointer) to the
    /// same object, so they all increment the same three fields in memory.
    /// </summary>
    private sealed class SharedCounters
    {
        public long Negative;
        public long Positive;
        public long Zero;

        public SignCounts ToSignCounts() => new() { Negative = Negative, Positive = Positive, Zero = Zero };
    }

    // System.Threading.Lock: a mutual-exclusion lock. Only one thread at a time can be
    // inside a `lock (Gate) { ... }` block; the others wait at the door.
    private static readonly Lock Gate = new();

    public static void Run(CommandLine cli)
    {
        string path = cli.GetString("--file", Paths.Numbers);
        int threadCount = cli.GetInt("--threads", Environment.ProcessorCount);
        string version = cli.GetString("--version", "all");
        Paths.RequireFile(path);

        Console.WriteLine($"Loading all numbers into an int array (not measured)...");
        int[] numbers = LoadNumbers(path);

        // Clean up the garbage left by loading (50 million temporary strings) now, so the
        // garbage collector does not wake up in the middle of a measurement.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        Console.WriteLine($"Counting {numbers.Length:N0} numbers with {threadCount} threads.");

        // A `switch` on strings. "all" = version 1 three times (as the lab asks), then 2 and 3.
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

    // Func<SignCounts> = "a function that takes nothing and returns a SignCounts".
    private static void Measure(string name, Func<SignCounts> countingMethod, int threads, CommandLine cli)
    {
        var stopwatch = Stopwatch.StartNew();
        SignCounts result = countingMethod();   // call the function we were given
        stopwatch.Stop();
        Report.Print("task1b", name, threads, stopwatch.Elapsed, result, cli);
    }

    // ---------------------------------------------------------------------------------
    // Version 1: shared counters, NO synchronisation  (a RACE CONDITION on purpose)
    // ---------------------------------------------------------------------------------
    private static SignCounts CountUnsynchronized(int[] numbers, int threadCount)
    {
        var shared = new SharedCounters();

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(numbers.Length, t, threadCount);
            for (long i = from; i < to; i++)
            {
                int value = numbers[i];
                // `shared.Negative++` looks like one step but the CPU does THREE:
                //   1) read Negative from memory into a register
                //   2) add 1 in the register
                //   3) write the register back to memory
                // If two threads both read 100, both compute 101 and both write 101,
                // two numbers were seen but the counter only grew by 1: an update is LOST.
                if (value < 0) shared.Negative++;
                else if (value > 0) shared.Positive++;
                else shared.Zero++;
            }
        });

        return shared.ToSignCounts();
    }

    // ---------------------------------------------------------------------------------
    // Version 2: shared counters protected by a lock
    // ---------------------------------------------------------------------------------
    private static SignCounts CountWithLock(int[] numbers, int threadCount)
    {
        var shared = new SharedCounters();

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(numbers.Length, t, threadCount);
            for (long i = from; i < to; i++)
            {
                int value = numbers[i];
                // Only one thread at a time may execute the read-add-write, so nothing is lost.
                // But ALL threads fight for the same lock 50 million times: they spend most of
                // their time waiting for each other instead of counting.
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

    // ---------------------------------------------------------------------------------
    // Version 3: a separate counter for each thread, added together at the end
    // ---------------------------------------------------------------------------------
    private static SignCounts CountPerThread(int[] numbers, int threadCount)
    {
        var perThread = new SignCounts[threadCount];

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(numbers.Length, t, threadCount);

            // `local` is a local variable of THIS thread: no other thread can see it,
            // so no lock is needed and the CPU can keep it in registers / its own cache.
            var local = new SignCounts();
            for (long i = from; i < to; i++)
                local.Add(numbers[i]);

            perThread[t] = local;   // shared memory is touched only ONCE, at the very end
        });

        // Join() inside RunAll guarantees every thread has finished before we add up.
        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;
        return total;
    }

    /// <summary>Reads numbers.txt line by line into an int[] (simple and slow; not measured).</summary>
    private static int[] LoadNumbers(string path)
    {
        // List<int> = a growable array. Giving a starting capacity avoids repeated resizing.
        var list = new List<int>(capacity: 50_000_000);

        // File.ReadLines returns the lines one by one (it does not load the whole file at once).
        foreach (string line in File.ReadLines(path))
            list.Add(int.Parse(line));   // "-42" -> -42

        return list.ToArray();           // copy into a plain fixed-size array
    }
}
