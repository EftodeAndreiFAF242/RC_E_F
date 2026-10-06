namespace Lab1;

/// <summary>Where the data files live (relative to the folder you run the program from: Lab1).</summary>
// `static class` = a class that is never instantiated with `new`; it only groups
// shared members, like a namespace for constants and helper functions.
public static class Paths
{
    public const string DataDir = "data";

    // Path.Combine joins folder names with the right separator ("data\numbers.txt" on Windows).
    public static readonly string Numbers  = Path.Combine(DataDir, "numbers.txt");
    public static readonly string Expected = Path.Combine(DataDir, "numbers.expected.txt");
    public static readonly string PartsDir = Path.Combine(DataDir, "parts");

    /// <summary>Stops with a clear message if a required input file does not exist.</summary>
    public static void RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"'{Path.GetFullPath(path)}' does not exist. Run 'gen' first to create the data.");
    }
}
