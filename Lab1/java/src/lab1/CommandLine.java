package lab1;

import java.util.HashMap;
import java.util.Locale;
import java.util.Map;

/**
 * A very small command-line reader (same rules as CommandLine.cs).
 * Example:  task1a --threads 8 --csv results.csv
 *   command() = "task1a",  options = { "--threads" -> "8", "--csv" -> "results.csv" }
 */
final class CommandLine {   // no `public`: visible only inside the package lab1

    // `final` on a field = it is assigned once (like `readonly` in C#).
    private final Map<String, String> options = new HashMap<>();
    private final String command;

    CommandLine(String[] args) {
        command = args.length > 0 ? args[0].toLowerCase(Locale.ROOT) : "help";

        for (int i = 1; i < args.length; i += 2) {
            String name = args[i];
            if (!name.startsWith("--"))
                throw new IllegalArgumentException("Unexpected '" + name + "'. Options look like: --threads 8");
            if (i + 1 >= args.length)
                throw new IllegalArgumentException("Option '" + name + "' needs a value.");
            options.put(name, args[i + 1]);
        }
    }

    String command() { return command; }

    String getString(String name, String defaultValue) {
        return options.getOrDefault(name, defaultValue);
    }

    /** Returns null when the option was not given (Java has no `string?`; any reference may be null). */
    String getOptionalString(String name) {
        return options.get(name);
    }

    int getInt(String name, int defaultValue) {
        String text = options.get(name);
        if (text == null) return defaultValue;
        try {
            return Integer.parseInt(text);
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("Option '" + name + "' must be a whole number, got '" + text + "'.");
        }
    }
}
