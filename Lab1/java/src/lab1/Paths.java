package lab1;

import java.io.FileNotFoundException;
import java.nio.file.Files;
import java.nio.file.Path;

/** Where the data files live, relative to the folder the program runs from (Lab1). Same as Paths.cs. */
final class Paths {
    private Paths() { }   // a private constructor: nobody can create a Paths object (like a C# static class)

    static final String NUMBERS   = "data/numbers.txt";
    static final String EXPECTED  = "data/numbers.expected.txt";
    static final String PARTS_DIR = "data/parts";

    /** Stops with a clear message if a required input file does not exist. */
    static void requireFile(Path path) throws FileNotFoundException {
        if (!Files.exists(path))
            throw new FileNotFoundException("'" + path.toAbsolutePath()
                    + "' does not exist. Run the C# program's 'gen' command first to create the data.");
    }
}
