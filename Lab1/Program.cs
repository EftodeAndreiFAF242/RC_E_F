// =============================================================================
//  Lab 1 - Concurrency in practice
//  Entry point: looks at the first word on the command line and runs that task.
//
//  Examples (from the Lab1 folder):
//      dotnet run -c Release -- gen                    generate the data files
//      dotnet run -c Release -- task1a --threads 8     Task 1a with 8 threads
//  Everything after `--` is passed to OUR program instead of to `dotnet`.
// =============================================================================

// This file uses "top-level statements": modern C# lets you write code directly in
// Program.cs, without `class Program { static void Main(string[] args) { ... } }`.
// The compiler generates that Main method for us, and `args` (the words typed after
// the program name) is available automatically.

using Lab1;   // our own classes live in the namespace "Lab1" (see the other files)

try
{
    // `var` = "let the compiler figure out the type". Here the type is CommandLine.
    var cli = new CommandLine(args);

    // `switch` picks one branch depending on the value of cli.Command.
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
// `catch ... when (...)` only catches the errors we expect from bad input;
// real bugs still crash loudly with a full stack trace, which helps debugging.
catch (Exception ex) when (ex is ArgumentException or FileNotFoundException
                               or DirectoryNotFoundException or FormatException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;   // non-zero exit code = "something went wrong" for scripts
}

// A "local function": a helper declared inside the top-level code.
static void PrintHelp()
{
    // @"..." is a verbatim string: it can span several lines and '\' is not special.
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

  task2    --threads N   [--dir data/parts]
           Each file is read with ONE read into a byte buffer, parsed and counted.

  info     Print CPU / OS / .NET information for the report.

Every task also accepts --csv <file> to append its result as a CSV row.");
}
