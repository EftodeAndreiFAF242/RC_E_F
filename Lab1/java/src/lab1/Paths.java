package lab1;

import java.io.FileNotFoundException;
import java.nio.file.Files;
import java.nio.file.Path;

/** Locations of the data files, relative to the Lab1 folder the program is run from. */
final class Paths {
    private Paths() { }

    static final String NUMBERS   = "data/numbers.txt";
    static final String EXPECTED  = "data/numbers.expected.txt";
    static final String PARTS_DIR = "data/parts";

    static void requireFile(Path path) throws FileNotFoundException {
        if (!Files.exists(path))
            throw new FileNotFoundException("'" + path.toAbsolutePath()
                    + "' does not exist. Run the C# program's 'gen' command first to create the data.");
    }
}
