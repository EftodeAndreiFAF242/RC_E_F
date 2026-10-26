package lab1;

import java.io.FileNotFoundException;
import java.nio.file.NoSuchFileException;

/**
 * Lab 1 - Concurrency in practice, JAVA version (the bonus task).
 *
 * It has the same commands and options as the C# program, reads the same data files
 * (created by the C# "gen" command) and writes the same CSV format with lang = "java",
 * so the two languages can be compared directly.
 *
 * Build and run from the Lab1 folder:
 *     javac -d java/out -encoding UTF-8 java/src/lab1/*.java
 *     java --enable-native-access=ALL-UNNAMED -cp java/out lab1.Main task1a --threads 8
 *
 * C# vs Java at a glance:
 *   C#  : the entry point can be "top-level statements" (no class needed).
 *   Java: every method lives in a class; the program starts at  public static void main(String[] args).
 */
public final class Main {

    public static void main(String[] args) throws Exception {
        try {
            CommandLine cli = new CommandLine(args);

            // A "switch expression" with arrows (Java 14+): no `break` needed, no fall-through.
            switch (cli.command()) {
                case "task1a" -> Task1a.run(cli);
                case "task1b" -> Task1b.run(cli);
                case "task1c" -> Task1c.run(cli);
                case "task2"  -> Task2.run(cli);
                case "info"   -> printInfo();
                default       -> printHelp();
            }
        }
        // Like the C# version: only errors caused by bad input are turned into a short message.
        // (NumberFormatException, thrown by Integer.parseInt, is a kind of IllegalArgumentException.)
        catch (IllegalArgumentException | FileNotFoundException | NoSuchFileException e) {
            System.err.println("Error: " + e.getMessage());
            System.exit(1);   // non-zero exit code = "something went wrong" for scripts
        }
    }

    private static void printInfo() {
        Runtime rt = Runtime.getRuntime();
        System.out.println("Logical processors : " + rt.availableProcessors());
        System.out.println("Max Java heap      : " + rt.maxMemory() / (1024 * 1024) + " MB");
        System.out.println("OS                 : " + System.getProperty("os.name") + " " + System.getProperty("os.version"));
        System.out.println("Runtime            : " + System.getProperty("java.vm.name") + " "
                           + System.getProperty("java.runtime.version") + " (" + System.getProperty("os.arch") + ")");
    }

    private static void printHelp() {
        // """ ... """ is a "text block" (Java 15+): a string that spans several lines.
        System.out.println("""
            Lab 1 - Concurrency in practice (Java version)

            Usage:  java --enable-native-access=ALL-UNNAMED -cp java/out lab1.Main <command> [--option value ...]

              task1a   --threads N   [--file data/numbers.txt]
              task1b   [--version 1|2|3|all] [--threads N]     (default N = logical processors)
              task1c   --workers N   [--batch 10000] [--capacity 100]   (--capacity 0 = unlimited)
              task2    --threads N   [--dir data/parts] [--label text]
              info     Print processor / OS / Java information.

            The data files are created by the C# program:  Lab1.exe gen
            Every task also accepts --csv <file> to append its result as a CSV row.""");
    }
}
