using System.Diagnostics;

namespace Lab1;

/// <summary>Prints one result with the process's peak memory, checks the totals and optionally appends a CSV row.</summary>
public static class Report
{
    public static void Print(string task, string variant, int threads, TimeSpan elapsed,
                             SignCounts counts, CommandLine cli,
                             double? ioMs = null, double? cpuMs = null, int? queueMax = null)
    {
        long peakBytes;
        using (Process me = Process.GetCurrentProcess())
            peakBytes = me.PeakWorkingSet64;   // the most physical RAM this process has used so far
        double peakMb = peakBytes / (1024.0 * 1024.0);

        string status = Check(counts, out string details);

        Console.WriteLine($"{task,-7} {variant,-14} threads = {threads,2}   " +
                          $"time = {elapsed.TotalMilliseconds,9:F1} ms   peak memory = {peakMb,7:F1} MB");
        Console.WriteLine($"        {counts}   -> {status} {details}");
        if (queueMax != null)
            Console.WriteLine($"        largest number of batches waiting in the queue = {queueMax}");

        string? csvPath = cli.GetOptionalString("--csv");
        if (csvPath != null)
            AppendCsv(csvPath, task, variant, threads, elapsed, peakMb, counts, status, ioMs, cpuMs, queueMax);
    }

    // Compares with data/numbers.expected.txt written by 'gen': every task must give the same totals.
    private static string Check(SignCounts counts, out string details)
    {
        details = "";
        if (!File.Exists(Paths.Expected))
        {
            details = "(no data/numbers.expected.txt to compare with)";
            return "UNCHECKED";
        }

        string[] words = File.ReadAllText(Paths.Expected)
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expected = new SignCounts
        {
            Negative = long.Parse(words[0]),
            Positive = long.Parse(words[1]),
            Zero     = long.Parse(words[2]),
        };

        if (counts.SameAs(expected)) return "CORRECT";

        details = $"(expected {expected})";
        return "WRONG";
    }

    private static void AppendCsv(string path, string task, string variant, int threads,
                                  TimeSpan elapsed, double peakMb, SignCounts c, string status,
                                  double? ioMs, double? cpuMs, int? queueMax)
    {
        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder != null) Directory.CreateDirectory(folder);

        bool isNewFile = !File.Exists(path);

        using var writer = new StreamWriter(path, append: true);
        if (isNewFile)
            writer.WriteLine("lang,task,variant,threads,time_ms,peak_memory_mb," +
                             "negative,positive,zero,total,check,io_ms,cpu_ms,queue_max");

        // An empty cell means the value is not measured by this task.
        writer.WriteLine($"csharp,{task},{variant},{threads},{elapsed.TotalMilliseconds:F1},{peakMb:F1}," +
                         $"{c.Negative},{c.Positive},{c.Zero},{c.Total},{status}," +
                         $"{ioMs?.ToString("F1")},{cpuMs?.ToString("F1")},{queueMax}");
    }
}
