using System.Diagnostics;

namespace Lab1;

/// <summary>Prints the result of one run, checks it, and optionally appends it to a CSV file.</summary>
public static class Report
{
    public static void Print(string task, string variant, int threads, TimeSpan elapsed,
                             SignCounts counts, CommandLine cli)
    {
        // Peak working set = the MOST physical RAM this process has used since it started.
        // `using (...)` releases the Process object (an OS handle) as soon as we're done with it.
        long peakBytes;
        using (Process me = Process.GetCurrentProcess())
            peakBytes = me.PeakWorkingSet64;
        double peakMb = peakBytes / (1024.0 * 1024.0);

        string status = Check(counts, out string details);

        // {x,-8} = left-align in 8 characters, {x,9:F1} = right-align in 9, 1 decimal.
        Console.WriteLine($"{task,-7} {variant,-14} threads = {threads,2}   " +
                          $"time = {elapsed.TotalMilliseconds,9:F1} ms   peak memory = {peakMb,7:F1} MB");
        Console.WriteLine($"        {counts}   -> {status} {details}");

        string? csvPath = cli.GetOptionalString("--csv");
        if (csvPath != null)
            AppendCsv(csvPath, task, variant, threads, elapsed, peakMb, counts, status);
    }

    /// <summary>
    /// Compares with data/numbers.expected.txt (written by 'gen'): the lab requires the same
    /// totals in every task. Returns "CORRECT", "WRONG" or "UNCHECKED".
    /// </summary>
    // `out string details`: a second return value, filled in by this method.
    private static string Check(SignCounts counts, out string details)
    {
        details = "";
        if (!File.Exists(Paths.Expected))
        {
            details = "(no data/numbers.expected.txt to compare with)";
            return "UNCHECKED";
        }

        // The file contains three numbers separated by spaces: "<negative> <positive> <zero>"
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
                                  TimeSpan elapsed, double peakMb, SignCounts c, string status)
    {
        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder != null) Directory.CreateDirectory(folder);   // does nothing if it already exists

        bool isNewFile = !File.Exists(path);

        // StreamWriter writes text to a file; append: true adds to the end instead of overwriting.
        using var writer = new StreamWriter(path, append: true);
        if (isNewFile)
            writer.WriteLine("task,variant,threads,time_ms,peak_memory_mb,negative,positive,zero,total,check");

        writer.WriteLine($"{task},{variant},{threads},{elapsed.TotalMilliseconds:F1},{peakMb:F1}," +
                         $"{c.Negative},{c.Positive},{c.Zero},{c.Total},{status}");
    }   // <- `using var` closes the file automatically here, at the end of the method
}
