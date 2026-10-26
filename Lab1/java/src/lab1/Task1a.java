package lab1;

import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;

/**
 * Task 1a: counting with threads. Same algorithm as Task1a.cs:
 * the file is cut into N equal BYTE ranges, one per thread; a line belongs to the thread whose
 * range contains the line's FIRST byte (see Task1a.cs for the full boundary explanation).
 */
final class Task1a {
    private Task1a() { }

    private static final int CHUNK_SIZE = 1 << 20;   // each thread reads 1 MB at a time

    static void run(CommandLine cli) throws Exception {
        Path path = Path.of(cli.getString("--file", Paths.NUMBERS));
        int threadCount = cli.getInt("--threads", Runtime.getRuntime().availableProcessors());
        Paths.requireFile(path);

        long fileSize = Files.size(path);
        SignCounts[] perThread = new SignCounts[threadCount];   // one result slot per thread

        long start = System.nanoTime();   // Java's high-precision timer (C#: Stopwatch)

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(fileSize, t, threadCount);
            perThread[t] = countRange(path, range[0], range[1]);   // each thread writes only its own slot
        });

        long elapsed = System.nanoTime() - start;

        SignCounts total = new SignCounts();
        for (SignCounts part : perThread)
            total.addAll(part);

        Report.print("task1a", "-", threadCount, elapsed, total, cli);
    }

    /** Counts the numbers on every line whose first byte lies in [start, end). */
    private static SignCounts countRange(Path path, long start, long end) throws Exception {
        SignCounts counts = new SignCounts();
        NumberParser parser = new NumberParser();
        byte[] buffer = new byte[CHUNK_SIZE];        // this thread's private buffer
        ByteBuffer wrapper = ByteBuffer.wrap(buffer); // a ByteBuffer "view" of the array, for FileChannel

        // Every thread opens its OWN channel to the file; try-with-resources closes it.
        try (FileChannel file = FileChannel.open(path, StandardOpenOption.READ)) {
            long position = start > 0 ? start - 1 : 0;   // one byte early, to see the byte before `start`
            boolean skipping = start > 0;                // true while skipping the previous thread's line

            while (true) {
                long remaining = end - position;
                int toRead = remaining > 0 ? (int) Math.min(CHUNK_SIZE, remaining) : 64;

                wrapper.clear().limit(toRead);
                // read(buffer, position) = read at an EXPLICIT file offset (C#: RandomAccess.Read).
                // It does not use or move a shared "current position" of the channel.
                int bytesRead = file.read(wrapper, position);

                if (bytesRead <= 0) {                    // -1 = end of the file
                    parser.finishLine(counts);           // the last line may have no '\n'
                    return counts;
                }

                for (int i = 0; i < bytesRead; i++) {
                    byte b = buffer[i];
                    long bytePosition = position + i;    // where this byte is in the whole file

                    if (skipping) {
                        if (b == '\n') {
                            skipping = false;
                            if (bytePosition + 1 >= end) return counts;   // no line starts in our range
                        }
                        continue;
                    }

                    // A line just ended; if the NEXT line starts at or after `end`, it isn't ours: stop.
                    if (parser.feed(b, counts) && bytePosition + 1 >= end)
                        return counts;
                }

                position += bytesRead;
            }
        }
    }
}
