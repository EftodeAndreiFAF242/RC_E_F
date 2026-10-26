package lab1;

import java.io.BufferedReader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;

/**
 * Task 1c: producer and consumers (same design as Task1c.cs).
 *
 *     [reader] --batches of 10,000 lines--> [ queue, max 100 batches ] --> [worker 1], [worker 2], ...
 *
 * C# vs Java: C#'s BlockingCollection can be "closed" (CompleteAdding). Java's BlockingQueue
 * cannot, so the reader closes it with a "poison pill": after the last batch it puts one
 * special END marker per worker. A worker that takes END knows nothing more is coming and stops.
 * Because the queue is first-in-first-out, every END comes after all the real batches.
 */
final class Task1c {
    private Task1c() { }

    // The poison pill. Workers compare with == (the SAME object), not with equals(),
    // so a real empty batch could never be mistaken for it.
    private static final List<String> END = new ArrayList<>();

    static void run(CommandLine cli) throws Exception {
        Path path = Path.of(cli.getString("--file", Paths.NUMBERS));
        int workerCount = cli.getInt("--workers", Runtime.getRuntime().availableProcessors());
        int batchSize = cli.getInt("--batch", 10_000);
        int capacity = cli.getInt("--capacity", 100);   // 0 = no size limit (question 6)
        Paths.requireFile(path);

        // ArrayBlockingQueue = a fixed-size blocking queue (put() waits while it is full).
        // LinkedBlockingQueue without a size = a queue with no limit (put() never waits).
        BlockingQueue<List<String>> queue = capacity > 0
                ? new ArrayBlockingQueue<>(capacity)
                : new LinkedBlockingQueue<>();

        SignCounts[] perWorker = new SignCounts[workerCount];
        Exception[] readerError = { null };   // a one-element array: lambdas can't assign local variables
        int[] queueMax = { 0 };               // the most batches ever waiting in the queue (set by the reader)

        long start = System.nanoTime();

        // The producer: one extra thread, created and started by hand.
        Thread reader = new Thread(() -> {
            try {
                queueMax[0] = readBatches(path, batchSize, workerCount, queue);
            } catch (Exception e) {
                readerError[0] = e;
            }
        });
        reader.start();

        // The consumers: runAll creates the workers and waits until all have finished.
        ThreadRunner.runAll(workerCount, w -> perWorker[w] = countBatches(queue));

        reader.join();
        long elapsed = System.nanoTime() - start;
        if (readerError[0] != null) throw readerError[0];

        SignCounts total = new SignCounts();
        for (SignCounts part : perWorker)
            total.addAll(part);

        String variant = "b" + batchSize + "-q" + (capacity > 0 ? String.valueOf(capacity) : "inf");
        Report.print("task1c", variant, workerCount, elapsed, total, cli, null, null, queueMax[0]);
    }

    /**
     * The producer: reads lines and puts them into the queue in batches, then one END per worker.
     * Returns the largest number of batches that were waiting in the queue at any moment.
     */
    private static int readBatches(Path path, int batchSize, int workerCount,
                                   BlockingQueue<List<String>> queue) throws Exception {
        int queueMax = 0;
        try (BufferedReader file = Files.newBufferedReader(path)) {
            List<String> batch = new ArrayList<>(batchSize);
            String line;
            while ((line = file.readLine()) != null) {
                batch.add(line);
                if (batch.size() == batchSize) {
                    queue.put(batch);                    // BLOCKS here while the queue is full
                    queueMax = Math.max(queueMax, queue.size());   // how full is it right now?
                    batch = new ArrayList<>(batchSize);  // a NEW list: the old one now belongs to a worker
                }
            }
            if (!batch.isEmpty())
                queue.put(batch);                        // the last, incomplete batch
            return queueMax;
        } finally {
            // "Close" the queue: even if reading failed, the workers must be told to stop.
            for (int i = 0; i < workerCount; i++)
                queue.put(END);
        }
    }

    /** A consumer: takes batches until it receives END. */
    private static SignCounts countBatches(BlockingQueue<List<String>> queue) throws Exception {
        SignCounts local = new SignCounts();   // private to this worker
        while (true) {
            List<String> batch = queue.take();  // WAITS here while the queue is empty
            if (batch == END) return local;
            for (String line : batch)
                local.add(Integer.parseInt(line));
        }
    }
}
