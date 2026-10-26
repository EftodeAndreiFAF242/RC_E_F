package lab1;

import java.io.BufferedReader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;

/** Task 1b: shared counters without synchronisation, shared counters with synchronized, and per-thread counters. */
final class Task1b {
    private Task1b() { }

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

    private static final Object GATE = new Object();   // in Java any object can be used as a lock

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
        System.gc();

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

    // Version 1, a race on purpose: counter++ is read-add-write, so two threads can write the same value and lose a count.
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

    // Version 2: correct, but all threads fight for the same lock 50 million times.
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

    // Version 3: every thread counts into its own object, and the results are added once after join.
    private static SignCounts countPerThread(int[] numbers, int threadCount) throws Exception {
        SignCounts[] perThread = new SignCounts[threadCount];

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(numbers.length, t, threadCount);
            SignCounts local = new SignCounts();
            for (int i = (int) range[0]; i < (int) range[1]; i++)
                local.add(numbers[i]);
            perThread[t] = local;
        });

        SignCounts total = new SignCounts();
        for (SignCounts part : perThread)
            total.addAll(part);
        return total;
    }

    // A plain int[] (4 bytes per number) instead of List<Integer>, which would need one object per number.
    private static int[] loadNumbers(Path path) throws Exception {
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
