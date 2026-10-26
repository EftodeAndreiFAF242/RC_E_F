package lab1;

import java.io.BufferedReader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;

/**
 * Task 1b: three ways to count (same as Task1b.cs).
 *   1. shared counters without synchronisation  -> WRONG (lost updates)
 *   2. shared counters protected by a lock      -> correct but slow
 *   3. a private counter per thread, summed at the end -> correct and fast
 */
final class Task1b {
    private Task1b() { }

    /** Versions 1 and 2 share ONE object of this class between all threads. */
    private static final class SharedCounters {
        long negative;
        long positive;
        long zero;

        SignCounts toSignCounts() {
            SignCounts c = new SignCounts();
            c.negative = negative;
            c.positive = positive;
            c.zero = zero;
            return c;
        }
    }

    // In Java ANY object can be used as a lock with the `synchronized` keyword (C#: `lock`).
    private static final Object GATE = new Object();

    /** A function that counts the numbers and returns the totals (C#: Func<SignCounts>). */
    @FunctionalInterface
    private interface Counter {
        SignCounts count() throws Exception;
    }

    static void run(CommandLine cli) throws Exception {
        Path path = Path.of(cli.getString("--file", Paths.NUMBERS));
        int threadCount = cli.getInt("--threads", Runtime.getRuntime().availableProcessors());
        String version = cli.getString("--version", "all");
        Paths.requireFile(path);

        System.out.println("Loading all numbers into an int array (not measured)...");
        int[] numbers = loadNumbers(path);
        System.gc();   // ask the garbage collector to clean up the 50 million temporary Strings now

        System.out.printf("Counting %,d numbers with %d threads.%n", numbers.length, threadCount);

        switch (version) {
            case "1" -> measure("v1-no-sync", () -> countUnsynchronized(numbers, threadCount), threadCount, cli);
            case "2" -> measure("v2-lock",    () -> countWithLock(numbers, threadCount),       threadCount, cli);
            case "3" -> measure("v3-local",   () -> countPerThread(numbers, threadCount),      threadCount, cli);
            case "all" -> {
                for (int run = 1; run <= 3; run++)
                    measure("v1-no-sync", () -> countUnsynchronized(numbers, threadCount), threadCount, cli);
                measure("v2-lock",  () -> countWithLock(numbers, threadCount),  threadCount, cli);
                measure("v3-local", () -> countPerThread(numbers, threadCount), threadCount, cli);
            }
            default -> throw new IllegalArgumentException("--version must be 1, 2, 3 or all");
        }
    }

    private static void measure(String name, Counter counter, int threads, CommandLine cli) throws Exception {
        long start = System.nanoTime();
        SignCounts result = counter.count();
        long elapsed = System.nanoTime() - start;
        Report.print("task1b", name, threads, elapsed, result, cli);
    }

    // ------------------------------------------------------------------------------
    // Version 1: shared counters, NO synchronisation (a race condition on purpose).
    // shared.negative++ is read -> add 1 -> write; two threads can read the same old value
    // and both write old+1, so one increment is LOST.
    // ------------------------------------------------------------------------------
    private static SignCounts countUnsynchronized(int[] numbers, int threadCount) throws Exception {
        SharedCounters shared = new SharedCounters();

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(numbers.length, t, threadCount);
            for (int i = (int) range[0]; i < (int) range[1]; i++) {
                int value = numbers[i];
                if (value < 0) shared.negative++;
                else if (value > 0) shared.positive++;
                else shared.zero++;
            }
        });

        return shared.toSignCounts();
    }

    // ------------------------------------------------------------------------------
    // Version 2: shared counters protected by a lock.
    // Only one thread at a time may be inside `synchronized (GATE) { ... }`.
    // ------------------------------------------------------------------------------
    private static SignCounts countWithLock(int[] numbers, int threadCount) throws Exception {
        SharedCounters shared = new SharedCounters();

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(numbers.length, t, threadCount);
            for (int i = (int) range[0]; i < (int) range[1]; i++) {
                int value = numbers[i];
                synchronized (GATE) {
                    if (value < 0) shared.negative++;
                    else if (value > 0) shared.positive++;
                    else shared.zero++;
                }
            }
        });

        return shared.toSignCounts();
    }

    // ------------------------------------------------------------------------------
    // Version 3: a separate counter for each thread, added together at the end.
    // ------------------------------------------------------------------------------
    private static SignCounts countPerThread(int[] numbers, int threadCount) throws Exception {
        SignCounts[] perThread = new SignCounts[threadCount];

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(numbers.length, t, threadCount);
            SignCounts local = new SignCounts();   // only THIS thread ever sees `local`
            for (int i = (int) range[0]; i < (int) range[1]; i++)
                local.add(numbers[i]);
            perThread[t] = local;                  // shared memory is touched once, at the end
        });

        SignCounts total = new SignCounts();
        for (SignCounts part : perThread)
            total.addAll(part);
        return total;
    }

    /** Reads numbers.txt line by line into an int[] (simple and slow; not measured). */
    private static int[] loadNumbers(Path path) throws Exception {
        // Java has no List<int> (generics work only with objects, so it would be List<Integer>,
        // one object per number = ~16 bytes each). A plain int[] that we grow ourselves is 4 bytes each.
        int[] numbers = new int[50_000_000];
        int count = 0;
        try (BufferedReader reader = Files.newBufferedReader(path)) {
            String line;
            while ((line = reader.readLine()) != null) {
                if (count == numbers.length) numbers = Arrays.copyOf(numbers, count * 2);
                numbers[count++] = Integer.parseInt(line);
            }
        }
        return count == numbers.length ? numbers : Arrays.copyOf(numbers, count);
    }
}
