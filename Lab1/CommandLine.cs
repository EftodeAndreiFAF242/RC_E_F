namespace Lab1;

/// <summary>Splits "command --name value ..." into the command word and a dictionary of options.</summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, string> _options = new();

    public string Command { get; }

    public CommandLine(string[] args)
    {
        Command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        for (int i = 1; i < args.Length; i += 2)
        {
            string name = args[i];
            if (!name.StartsWith("--"))
                throw new ArgumentException($"Unexpected '{name}'. Options look like: --threads 8");
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Option '{name}' needs a value.");

            _options[name] = args[i + 1];
        }
    }

    public string GetString(string name, string defaultValue) =>
        _options.TryGetValue(name, out var value) ? value : defaultValue;

    public string? GetOptionalString(string name) =>
        _options.TryGetValue(name, out var value) ? value : null;

    public int GetInt(string name, int defaultValue)
    {
        if (!_options.TryGetValue(name, out var text)) return defaultValue;
        if (!int.TryParse(text, out int number))
            throw new ArgumentException($"Option '{name}' must be a whole number, got '{text}'.");
        return number;
    }
}
