namespace Lab1;

/// <summary>Starts N real OS threads and waits for all of them, and splits a range of work into N equal parts.</summary>
public static class ThreadRunner
{
    public static void RunAll(int count, Action<int> body)
    {
        if (count < 1) throw new ArgumentException("The number of threads must be at least 1.");

        var threads = new Thread[count];

        for (int t = 0; t < count; t++)
        {
            int index = t;   // the lambda captures variables, so each thread needs its own copy of t
            threads[t] = new Thread(() => body(index));
            threads[t].Start();
        }

        foreach (Thread thread in threads)
            thread.Join();
    }

    /// <summary>Piece number index of [0, length) cut into parts pieces that never overlap, e.g. 10 in 3: [0,3) [3,6) [6,10).</summary>
    public static (long Start, long End) Split(long length, int index, int parts) =>
        (length * index / parts, length * (index + 1) / parts);
}
