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
 * Task 1c: a reader thread fills a bounded blocking queue with batches and N workers count them; Java's queue
 * cannot be closed, so the reader ends with one END "poison pill" per worker.
 */
final class Task1c {
    private Task1c() { }

    // Compared with == (the same object), so a real empty batch can never be mistaken for it.
    private static final List<String> END = new ArrayList<>();

    static void run(CommandLine cli) throws Exception {
        Path path = Path.of(cli.getString("--file", Paths.NUMBERS));
        int workerCount = cli.getInt("--workers", Runtime.getRuntime().availableProcessors());
        int batchSize = cli.getInt("--batch", 10_000);
        int capacity = cli.getInt("--capacity", 100);   // 0 = no size limit
        Paths.requireFile(path);

        BlockingQueue<List<String>> queue = capacity > 0
                ? new ArrayBlockingQueue<>(capacity)
                : new LinkedBlockingQueue<>();

        SignCounts[] perWorker = new SignCounts[workerCount];
        Exception[] readerError = { null };   // one-element arrays, because a lambda cannot assign local variables
        int[] queueMax = { 0 };

        long start = System.nanoTime();

        Thread reader = new Thread(() -> {
            try {
                queueMax[0] = readBatches(path, batchSize, workerCount, queue);
            } catch (Exception e) {
                readerError[0] = e;
            }
        });
        reader.start();

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

    private static int readBatches(Path path, int batchSize, int workerCount,
                                   BlockingQueue<List<String>> queue) throws Exception {
        int queueMax = 0;
        try (BufferedReader file = Files.newBufferedReader(path)) {
            List<String> batch = new ArrayList<>(batchSize);
            String line;
            while ((line = file.readLine()) != null) {
                batch.add(line);
                if (batch.size() == batchSize) {
                    queue.put(batch);                               // blocks while the queue is full
                    queueMax = Math.max(queueMax, queue.size());
                    batch = new ArrayList<>(batchSize);             // the old list now belongs to a worker
                }
            }
            if (!batch.isEmpty())
                queue.put(batch);
            return queueMax;
        } finally {
            for (int i = 0; i < workerCount; i++)   // in finally, so the workers stop even if reading failed
                queue.put(END);
        }
    }

    private static SignCounts countBatches(BlockingQueue<List<String>> queue) throws Exception {
        SignCounts local = new SignCounts();
        while (true) {
            List<String> batch = queue.take();   // waits while the queue is empty
            if (batch == END) return local;
            for (String line : batch)
                local.add(Integer.parseInt(line));
        }
    }
}
