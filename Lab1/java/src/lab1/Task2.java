package lab1;

import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

/**
 * Task 2: each thread reads its share of the 8 files, every file with one read into a reused direct ByteBuffer
 * (the OS reads straight into it), and measures its I/O time and its parsing time.
 */
final class Task2 {
    private Task2() { }

    static void run(CommandLine cli) throws Exception {
        Path dir = Path.of(cli.getString("--dir", Paths.PARTS_DIR));
        int threadCount = cli.getInt("--threads", 1);
        String label = cli.getString("--label", "-");
        if (!"on".equals(cli.getString("--cache", "on")))
            throw new IllegalArgumentException("The Java version supports only --cache on.");
        if (!Files.isDirectory(dir))
            throw new IllegalArgumentException("Folder '" + dir + "' not found.");

        List<Path> files = new ArrayList<>();
        try (DirectoryStream<Path> stream = Files.newDirectoryStream(dir, "numbers_*.txt")) {
            for (Path p : stream) files.add(p);
        }
        Collections.sort(files);
        if (files.isEmpty())
            throw new IllegalArgumentException("No numbers_*.txt files in '" + dir + "'.");

        System.out.println(files.size() + " files in " + dir.toAbsolutePath() + ", cache on");

        SignCounts[] perThread = new SignCounts[threadCount];
        long[] ioNanos = new long[threadCount];
        long[] cpuNanos = new long[threadCount];
        long[] bytesRead = new long[threadCount];

        long start = System.nanoTime();

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(files.size(), t, threadCount);
            int from = (int) range[0], to = (int) range[1];
            SignCounts local = new SignCounts();

            long largest = 0;
            for (int i = from; i < to; i++)
                largest = Math.max(largest, Files.size(files.get(i)));
            ByteBuffer buffer = ByteBuffer.allocateDirect((int) largest);

            for (int i = from; i < to; i++) {
                long t0 = System.nanoTime();
                int filled;
                try (FileChannel file = FileChannel.open(files.get(i), StandardOpenOption.READ)) {
                    int length = (int) file.size();
                    buffer.clear().limit(length);
                    while (buffer.hasRemaining())   // one read call; the loop is only a safety net
                        if (file.read(buffer, buffer.position()) < 0) break;
                    filled = buffer.position();
                }
                long t1 = System.nanoTime();

                local.addAll(NumberParser.countAll(buffer, filled));

                long t2 = System.nanoTime();
                ioNanos[t] += t1 - t0;
                cpuNanos[t] += t2 - t1;
                bytesRead[t] += filled;
            }
            perThread[t] = local;
        });

        long elapsed = System.nanoTime() - start;

        SignCounts total = new SignCounts();
        long ioSum = 0, cpuSum = 0, bytes = 0;
        for (int t = 0; t < threadCount; t++) {
            total.addAll(perThread[t]);
            ioSum += ioNanos[t];
            cpuSum += cpuNanos[t];
            bytes += bytesRead[t];
        }
        double ioMs = ioSum / 1_000_000.0, cpuMs = cpuSum / 1_000_000.0;
        double megabytes = bytes / (1024.0 * 1024.0);

        Report.print("task2", label, threadCount, elapsed, total, cli, ioMs, cpuMs, null);
        System.out.println(String.format(Locale.ROOT,
                "        I/O (open + read) = %9.1f ms   CPU (parse) = %8.1f ms   (sums over all threads; I/O share = %.1f %%)",
                ioMs, cpuMs, 100 * ioMs / (ioMs + cpuMs)));
        System.out.println(String.format(Locale.ROOT, "        %.1f MB read -> %.1f MB/s overall",
                megabytes, megabytes / (elapsed / 1e9)));
    }
}
