using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab1;

/// <summary>
/// Task 1c: one reader thread puts batches of lines into a bounded blocking queue, and N worker threads
/// take the batches and count them, each into its own counters.
/// </summary>
public static class Task1c
{
    public static void Run(CommandLine cli)
    {
        string path = cli.GetString("--file", Paths.Numbers);
        int workerCount = cli.GetInt("--workers", Environment.ProcessorCount);
        int batchSize = cli.GetInt("--batch", 10_000);
        int capacity = cli.GetInt("--capacity", 100);   // 0 = no size limit
        Paths.RequireFile(path);

        using var queue = capacity > 0
            ? new BlockingCollection<List<string>>(boundedCapacity: capacity)
            : new BlockingCollection<List<string>>();

        var perWorker = new SignCounts[workerCount];

        // The most batches ever waiting: close to 0 means the workers wait for the reader, close to the capacity the opposite.
        int queueMax = 0;

        var stopwatch = Stopwatch.StartNew();

        var reader = new Thread(() => queueMax = ReadBatches(path, batchSize, queue));
        reader.Start();

        ThreadRunner.RunAll(workerCount, w => perWorker[w] = CountBatches(queue));

        reader.Join();
        stopwatch.Stop();

        var total = new SignCounts();
        foreach (SignCounts part in perWorker)
            total += part;

        string variant = $"b{batchSize}-q{(capacity > 0 ? capacity.ToString() : "inf")}";
        Report.Print("task1c", variant, workerCount, stopwatch.Elapsed, total, cli, queueMax: queueMax);
    }

    private static int ReadBatches(string path, int batchSize, BlockingCollection<List<string>> queue)
    {
        int queueMax = 0;
        try
        {
            using var file = new StreamReader(path);
            var batch = new List<string>(batchSize);

            string? line;
            while ((line = file.ReadLine()) != null)
            {
                batch.Add(line);
                if (batch.Count == batchSize)
                {
                    queue.Add(batch);                             // blocks while the queue is full
                    queueMax = Math.Max(queueMax, queue.Count);
                    batch = new List<string>(batchSize);          // the old list now belongs to a worker
                }
            }

            if (batch.Count > 0)
                queue.Add(batch);
            return queueMax;
        }
        finally
        {
            queue.CompleteAdding();   // in finally, so the workers are never left waiting forever
        }
    }

    private static SignCounts CountBatches(BlockingCollection<List<string>> queue)
    {
        var local = new SignCounts();

        // Each batch goes to exactly one worker; the loop waits while the queue is empty and ends after CompleteAdding.
        foreach (List<string> batch in queue.GetConsumingEnumerable())
        {
            foreach (string line in batch)
                local.Add(int.Parse(line));
        }

        return local;
    }
}
