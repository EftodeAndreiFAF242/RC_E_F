namespace Lab1;

/// <summary>Locations of the data files, relative to the Lab1 folder the program is run from.</summary>
public static class Paths
{
    public const string DataDir = "data";

    public static readonly string Numbers  = Path.Combine(DataDir, "numbers.txt");
    public static readonly string Expected = Path.Combine(DataDir, "numbers.expected.txt");
    public static readonly string PartsDir = Path.Combine(DataDir, "parts");

    public static void RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"'{Path.GetFullPath(path)}' does not exist. Run 'gen' first to create the data.");
    }
}
