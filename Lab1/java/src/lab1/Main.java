package lab1;

import java.io.FileNotFoundException;
import java.nio.file.NoSuchFileException;

/** Java version of Lab 1 (the bonus): same commands, options, data files and CSV format as the C# program. */
public final class Main {

    public static void main(String[] args) throws Exception {
        try {
            CommandLine cli = new CommandLine(args);

            switch (cli.command()) {
                case "task1a" -> Task1a.run(cli);
                case "task1b" -> Task1b.run(cli);
                case "task1c" -> Task1c.run(cli);
                case "task2"  -> Task2.run(cli);
                case "info"   -> printInfo();
                default       -> printHelp();
            }
        }
        // Only errors caused by bad input become a short message, like in the C# version.
        catch (IllegalArgumentException | FileNotFoundException | NoSuchFileException e) {
            System.err.println("Error: " + e.getMessage());
            System.exit(1);
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
