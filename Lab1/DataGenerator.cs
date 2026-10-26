namespace Lab1;

/// <summary>
/// Writes numbers.txt, the same numbers cut in order into 8 part files for Task 2, and the correct totals
/// that every task checks its own result against.
/// </summary>
public static class DataGenerator
{
    public static void Run(CommandLine cli)
    {
        int count = cli.GetInt("--count", 50_000_000);
        int parts = cli.GetInt("--parts", 8);
        int seed  = cli.GetInt("--seed", 12345);   // a fixed seed makes the file the same on every run

        Directory.CreateDirectory(Paths.DataDir);
        Directory.CreateDirectory(Paths.PartsDir);

        var random = new Random(seed);
        var counts = new SignCounts();
        int linesPerPart = count / parts;

        Console.WriteLine($"Writing {count:N0} numbers to {Paths.Numbers} and {parts} part files...");

        // UTF-8 without a BOM and '\n' line endings, so the files contain only digits, '-' and '\n'.
        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var all = new StreamWriter(Paths.Numbers, append: false, encoding, bufferSize: 1 << 20);
        all.NewLine = "\n";

        StreamWriter? part = null;
        int partNumber = 0;

        try
        {
            for (int i = 0; i < count; i++)
            {
                if (i % linesPerPart == 0 && partNumber < parts)
                {
                    part?.Dispose();
                    partNumber++;
                    string partPath = Path.Combine(Paths.PartsDir, $"numbers_{partNumber}.txt");
                    part = new StreamWriter(partPath, append: false, encoding, bufferSize: 1 << 20)
                    {
                        NewLine = "\n",
                    };
                }

                int value = random.Next(-1_000_000, 1_000_001);   // the upper bound is exclusive

                all.WriteLine(value);
                part!.WriteLine(value);
                counts.Add(value);
            }
        }
        finally
        {
            part?.Dispose();
        }

        File.WriteAllText(Paths.Expected, $"{counts.Negative} {counts.Positive} {counts.Zero}");

        Console.WriteLine($"Done. Correct totals: {counts}");
        Console.WriteLine($"Saved them to {Paths.Expected}; every task compares its result with this file.");
    }
}
