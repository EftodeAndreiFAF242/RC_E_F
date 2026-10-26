package lab1;

import java.io.IOException;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.Locale;

/** Prints the result of one run, checks it, and optionally appends it to a CSV file (same as Report.cs). */
final class Report {
    private Report() { }

    /** Task 1a/1b: no extra measurements. */
    static void print(String task, String variant, int threads, long elapsedNanos,
                      SignCounts counts, CommandLine cli) throws IOException {
        print(task, variant, threads, elapsedNanos, counts, cli, null, null, null);
    }

    /**
     * Java has no optional parameters, so the short version above calls this one ("overloading").
     * ioMs / cpuMs (Task 2) are `Double` and queueMax (Task 1c) is `Integer`: with a capital
     * letter they are objects, so they may be null ("not measured"), unlike `double` / `int`.
     */
    static void print(String task, String variant, int threads, long elapsedNanos, SignCounts counts,
                      CommandLine cli, Double ioMs, Double cpuMs, Integer queueMax) throws IOException {
        double timeMs = elapsedNanos / 1_000_000.0;
        double peakMb = PeakMemory.peakBytes() / (1024.0 * 1024.0);
        String[] details = { "" };
        String status = check(counts, details);

        System.out.println(String.format(Locale.ROOT, "%-7s %-14s threads = %2d   time = %9.1f ms   peak memory = %7.1f MB",
                                         task, variant, threads, timeMs, peakMb));
        System.out.println("        " + counts + "   -> " + status + " " + details[0]);
        if (queueMax != null)
            System.out.println("        largest number of batches waiting in the queue = " + queueMax);

        String csvPath = cli.getOptionalString("--csv");
        if (csvPath != null)
            appendCsv(Path.of(csvPath), task, variant, threads, timeMs, peakMb, counts, status, ioMs, cpuMs, queueMax);
    }

    /** Compares with data/numbers.expected.txt. Returns CORRECT, WRONG or UNCHECKED. */
    private static String check(SignCounts counts, String[] details) throws IOException {
        Path expectedFile = Path.of(Paths.EXPECTED);
        if (!Files.exists(expectedFile)) {
            details[0] = "(no data/numbers.expected.txt to compare with)";
            return "UNCHECKED";
        }
        String[] words = Files.readString(expectedFile).trim().split("\\s+");
        SignCounts expected = new SignCounts();
        expected.negative = Long.parseLong(words[0]);
        expected.positive = Long.parseLong(words[1]);
        expected.zero     = Long.parseLong(words[2]);

        if (counts.sameAs(expected)) return "CORRECT";
        details[0] = "(expected " + expected + ")";
        return "WRONG";
    }

    private static void appendCsv(Path path, String task, String variant, int threads, double timeMs,
                                  double peakMb, SignCounts c, String status, Double ioMs, Double cpuMs,
                                  Integer queueMax) throws IOException {
        Path folder = path.toAbsolutePath().getParent();
        if (folder != null) Files.createDirectories(folder);
        boolean isNewFile = !Files.exists(path);

        // try-with-resources = C#'s `using`: the file is closed at the end of the block.
        try (PrintWriter out = new PrintWriter(Files.newBufferedWriter(path, StandardCharsets.UTF_8,
                StandardOpenOption.CREATE, StandardOpenOption.APPEND))) {
            if (isNewFile)
                out.print("lang,task,variant,threads,time_ms,peak_memory_mb,negative,positive,zero,total,check,io_ms,cpu_ms,queue_max\n");
            out.print(String.format(Locale.ROOT, "java,%s,%s,%d,%.1f,%.1f,%d,%d,%d,%d,%s,%s,%s,%s\n",
                    task, variant, threads, timeMs, peakMb, c.negative, c.positive, c.zero, c.total(), status,
                    ioMs == null ? "" : String.format(Locale.ROOT, "%.1f", ioMs),
                    cpuMs == null ? "" : String.format(Locale.ROOT, "%.1f", cpuMs),
                    queueMax == null ? "" : queueMax.toString()));
        }
    }
}
