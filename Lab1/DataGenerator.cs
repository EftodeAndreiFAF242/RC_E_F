namespace Lab1;

/// <summary>
/// Section 2 "Data" of the lab:
///  - data/numbers.txt           50,000,000 random integers in [-1,000,000 .. 1,000,000], one per line
///  - data/parts/numbers_1..8.txt numbers.txt split IN ORDER into 8 files of 6,250,000 lines (Task 2)
///  - data/numbers.expected.txt  the correct totals, so every task can check itself
/// Each number is written to numbers.txt AND to the current part file at the same moment,
/// so the parts are exactly numbers.txt cut into 8 consecutive pieces.
/// </summary>
public static class DataGenerator
{
    public static void Run(CommandLine cli)
    {
        int count = cli.GetInt("--count", 50_000_000);   // '_' in numbers is only for readability
        int parts = cli.GetInt("--parts", 8);
        int seed  = cli.GetInt("--seed", 12345);          // same seed => same file every time

        Directory.CreateDirectory(Paths.DataDir);
        Directory.CreateDirectory(Paths.PartsDir);

        // Random with a fixed seed produces the same "random" sequence on every run,
        // so your results are reproducible.
        var random = new Random(seed);
        var counts = new SignCounts();
        int linesPerPart = count / parts;                  // 50,000,000 / 8 = 6,250,000

        Console.WriteLine($"Writing {count:N0} numbers to {Paths.Numbers} and {parts} part files...");

        // Plain text with no "BOM" (3 invisible marker bytes some editors put at the start of
        // a file), so the files contain ONLY digits, '-' and '\n'.
        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // bufferSize: collect 1 MB in memory before each write to disk (far fewer, bigger writes).
        // `1 << 20` = 1 shifted left 20 bits = 2^20 = 1,048,576 bytes = 1 MB.
        using var all = new StreamWriter(Paths.Numbers, append: false, encoding, bufferSize: 1 << 20);
        all.NewLine = "\n";   // one '\n' per line (Windows would otherwise use "\r\n")

        StreamWriter? part = null;   // the part file we are currently writing to
        int partNumber = 0;

        try
        {
            for (int i = 0; i < count; i++)
            {
                // Time to start the next part file? (the last part also takes any leftover lines)
                if (i % linesPerPart == 0 && partNumber < parts)
                {
                    part?.Dispose();   // close the previous part (?. = only if it isn't null)
                    partNumber++;
                    string partPath = Path.Combine(Paths.PartsDir, $"numbers_{partNumber}.txt");
                    part = new StreamWriter(partPath, append: false, encoding, bufferSize: 1 << 20)
                    {
                        NewLine = "\n",
                    };
                }

                // Next(min, max) returns min <= x < max, so the upper bound is 1,000,001.
                int value = random.Next(-1_000_000, 1_000_001);

                all.WriteLine(value);
                part!.WriteLine(value);   // `!` tells the compiler "trust me, part is not null here"
                counts.Add(value);
            }
        }
        finally
        {
            part?.Dispose();   // `finally` runs even if an error happened, so the file is always closed
        }

        // Save the correct answer: "<negative> <positive> <zero>"
        File.WriteAllText(Paths.Expected, $"{counts.Negative} {counts.Positive} {counts.Zero}");

        Console.WriteLine($"Done. Correct totals: {counts}");
        Console.WriteLine($"Saved them to {Paths.Expected}; every task compares its result with this file.");
    }
}
