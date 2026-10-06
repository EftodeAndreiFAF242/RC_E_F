namespace Lab1;   // "file-scoped namespace": everything in this file belongs to Lab1

/// <summary>
/// A very small command-line reader.
/// Example:  Lab1 task1a --threads 8 --csv results.csv
///   Command = "task1a"
///   options = { "--threads" -> "8", "--csv" -> "results.csv" }
/// </summary>
// `sealed` = no other class may inherit from this one (we don't need inheritance here).
public sealed class CommandLine
{
    // Dictionary<TKey, TValue> = a hash map. `readonly` = the field is assigned only in the constructor.
    private readonly Dictionary<string, string> _options = new();

    // A property with only `get`: it can be read from outside but only set in the constructor.
    public string Command { get; }

    // The constructor: runs when someone writes `new CommandLine(args)`.
    public CommandLine(string[] args)
    {
        // condition ? valueIfTrue : valueIfFalse   (the "ternary" operator)
        Command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        for (int i = 1; i < args.Length; i += 2)
        {
            string name = args[i];
            if (!name.StartsWith("--"))
                throw new ArgumentException($"Unexpected '{name}'. Options look like: --threads 8");
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Option '{name}' needs a value.");

            _options[name] = args[i + 1];   // store "--threads" -> "8"
        }
    }

    // `out var value`: TryGetValue returns true/false AND gives us the value through `value`.
    public string GetString(string name, string defaultValue) =>
        _options.TryGetValue(name, out var value) ? value : defaultValue;

    // `string?` = this method may return null (when the option was not given).
    public string? GetOptionalString(string name) =>
        _options.TryGetValue(name, out var value) ? value : null;

    public int GetInt(string name, int defaultValue)
    {
        if (!_options.TryGetValue(name, out var text)) return defaultValue;
        if (!int.TryParse(text, out int number))   // TryParse = parse without throwing on bad input
            throw new ArgumentException($"Option '{name}' must be a whole number, got '{text}'.");
        return number;
    }
}
