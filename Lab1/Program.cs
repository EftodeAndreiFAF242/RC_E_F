// Lab 1 - Concurrency in practice: runs the task named by the first command-line word (gen, task1a, ...).
using Lab1;

try
{
    var cli = new CommandLine(args);

    switch (cli.Command)
    {
        case "gen":    DataGenerator.Run(cli); break;
        case "task1a": Task1a.Run(cli);        break;
        case "task1b": Task1b.Run(cli);        break;
        case "task1c": Task1c.Run(cli);        break;
        case "task2":  Task2.Run(cli);         break;
        case "info":   SystemInfo.Print();     break;
        default:       PrintHelp();            break;
    }
}
// Only errors caused by bad input become a short message; real bugs still crash with a full stack trace.
catch (Exception ex) when (ex is ArgumentException or FileNotFoundException
                               or DirectoryNotFoundException or FormatException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;
}

static void PrintHelp()
{
    Console.WriteLine(@"Lab 1 - Concurrency in practice

Usage:  Lab1 <command> [--option value ...]

  gen      [--count 50000000] [--parts 8] [--seed 12345]
           Creates data/numbers.txt, data/parts/numbers_1..8.txt and
           data/numbers.expected.txt (the correct totals, used to check every task).

  task1a   --threads N   [--file data/numbers.txt]
           Split the file into N byte ranges, one thread per range.

  task1b   [--version 1|2|3|all] [--threads N]     (default N = logical processors)
           1 = shared counters, no sync   2 = shared counters + lock   3 = per-thread counters
           'all' runs version 1 three times, then 2 and 3.

  task1c   --workers N   [--batch 10000] [--capacity 100]   (--capacity 0 = unlimited)
           1 reader thread + N worker threads connected by a bounded blocking queue.

  task2    --threads N   [--dir data/parts] [--cache on|off] [--label text]
           Each file is read with ONE read into a byte buffer, parsed and counted.
           Also prints how long the threads waited for the disk (I/O) and parsed (CPU).
           --cache off reads straight from the device, bypassing the Windows file cache.
           --label only names the run in the output/CSV (run_task2.ps1 uses cold / warm).

  info     Print CPU / OS / .NET information for the report.

Every task also accepts --csv <file> to append its result as a CSV row.");
}
