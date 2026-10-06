namespace Lab1;

/// <summary>Helpers shared by all tasks for creating threads and dividing work.</summary>
public static class ThreadRunner
{
    /// <summary>
    /// Creates <paramref name="count"/> real operating-system threads. Thread number t runs
    /// body(t). The method returns only after ALL threads have finished.
    /// </summary>
    // Action<int> = "a function that takes an int and returns nothing".
    // Callers pass a lambda, e.g.  t => Console.WriteLine(t)
    public static void RunAll(int count, Action<int> body)
    {
        if (count < 1) throw new ArgumentException("The number of threads must be at least 1.");

        var threads = new Thread[count];

        for (int t = 0; t < count; t++)
        {
            // IMPORTANT C# detail: the lambda below "captures" variables, it does not copy them.
            // All iterations of a `for` loop share the SAME variable t, so by the time a thread
            // actually starts, t may already have changed. `index` is declared inside the loop
            // body, so each iteration gets its own fresh copy -> each thread sees its own number.
            int index = t;

            // new Thread(...) only CREATES the thread; nothing runs yet.
            threads[t] = new Thread(() => body(index));
            threads[t].Start();   // now the OS schedules it on some core and it starts running
        }

        // Join() blocks the calling thread until that thread has finished.
        foreach (Thread thread in threads)
            thread.Join();
    }

    /// <summary>
    /// Divides the range [0, length) into <paramref name="parts"/> nearly equal pieces and
    /// returns piece number <paramref name="index"/> as [Start, End) (End not included).
    /// Example: length 10, 3 parts -> [0,3), [3,6), [6,10). The pieces never overlap and
    /// together cover everything.
    /// </summary>
    // (long Start, long End) is a "tuple": a quick way to return two values at once.
    public static (long Start, long End) Split(long length, int index, int parts) =>
        (length * index / parts, length * (index + 1) / parts);
}
