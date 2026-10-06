using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab1;

/// <summary>
/// Task 1c: producer and consumers.
///
///     [reader thread] --batches of 10,000 lines--> [ queue, max 100 batches ] --> [worker 1]
///                                                                             --> [worker 2]
///                                                                             --> ...
/// - The reader reads numbers.txt line by line, packs the lines into batches and puts them
///   in the queue. When the queue is full, Add() BLOCKS: the reader waits.
/// - Each worker repeatedly takes the next batch and counts it. When the queue is empty,
///   the worker BLOCKS: it waits.
/// - At the end of the file the reader "closes" the queue (CompleteAdding). The workers
///   finish the remaining batches and then stop.
/// </summary>
public static class Task1c
{
    public static void Run(CommandLine cli)
    {
        string path = cli.GetString("--file", Paths.Numbers);
        int workerCount = cli.GetInt("--workers", Environment.ProcessorCount);
        int batchSize = cli.GetInt("--batch", 10_000);
        int capacity = cli.GetInt("--capacity", 100);   // 0 = no size limit (for question 6)
        Paths.RequireFile(path);

        // BlockingCollection<T> is .NET's ready-made thread-safe BLOCKING queue.
        // With boundedCapacity it holds at most that many items. Each item here is one
        // batch = a List<string> of up to 10,000 lines.
        using var queue = capacity > 0
            ? new BlockingCollection<List<string>>(boundedCapacity: capacity)
            : new BlockingCollection<List<string>>();

        var perWorker = new SignCounts[workerCount];

        var stopwatch = Stopwatch.StartNew();

        // The producer: one extra thread, created and started by hand.
        var reader = new Thread(() => ReadBatches(path, batchSize, queue));
        reader.Start();

        // The consumers: RunAll creates the workers and waits until all have finished.
        ThreadRunner.RunAll(workerCount, w => perWorker[w] = CountBatches(queue));

        reader.Join();
        stopwatch.Stop();

        var total = new SignCounts();
        foreach (SignCounts part in perWorker)
            total += part;

        string variant = $"b{batchSize}-q{(capacity > 0 ? capacity.ToString() : "inf")}";
        Report.Print("task1c", variant, workerCount, stopwatch.Elapsed, total, cli);
    }

    /// <summary>The producer: reads lines and pushes them into the queue in batches.</summary>
    private static void ReadBatches(string path, int batchSize, BlockingCollection<List<string>> queue)
    {
        try
        {
            // StreamReader turns the file's bytes into text, line by line.
            using var file = new StreamReader(path);
            var batch = new List<string>(batchSize);

            // ReadLine returns null at the end of the file, so the type is `string?`.
            string? line;
            while ((line = file.ReadLine()) != null)
            {
                batch.Add(line);
                if (batch.Count == batchSize)
                {
                    queue.Add(batch);                      // BLOCKS here while the queue is full
                    batch = new List<string>(batchSize);   // a NEW list: the old one now belongs to a worker
                }
            }

            if (batch.Count > 0)
                queue.Add(batch);                          // the last, incomplete batch
        }
        finally
        {
            // "Close" the queue. It's in `finally` so that even if reading fails, the workers
            // are told that nothing more is coming instead of waiting forever.
            queue.CompleteAdding();
        }
    }

    /// <summary>A consumer: takes batches until the queue is closed AND empty.</summary>
    private static SignCounts CountBatches(BlockingCollection<List<string>> queue)
    {
        var local = new SignCounts();   // private to this worker, like version 3 of Task 1b

        // GetConsumingEnumerable() hands out each batch to exactly ONE worker. It waits while
        // the queue is empty, and the loop ends once CompleteAdding() was called and the queue
        // has been emptied.
        foreach (List<string> batch in queue.GetConsumingEnumerable())
        {
            foreach (string line in batch)
                local.Add(int.Parse(line));
        }

        return local;
    }
}
