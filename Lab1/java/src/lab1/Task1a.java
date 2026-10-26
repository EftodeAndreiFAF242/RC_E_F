package lab1;

import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;

/** Task 1a: the file is cut into N byte ranges and a line belongs to the thread whose range holds its first byte. */
final class Task1a {
    private Task1a() { }

    private static final int CHUNK_SIZE = 1 << 20;

    static void run(CommandLine cli) throws Exception {
        Path path = Path.of(cli.getString("--file", Paths.NUMBERS));
        int threadCount = cli.getInt("--threads", Runtime.getRuntime().availableProcessors());
        Paths.requireFile(path);

        long fileSize = Files.size(path);
        SignCounts[] perThread = new SignCounts[threadCount];

        long start = System.nanoTime();

        ThreadRunner.runAll(threadCount, t -> {
            long[] range = ThreadRunner.split(fileSize, t, threadCount);
            perThread[t] = countRange(path, range[0], range[1]);
        });

        long elapsed = System.nanoTime() - start;

        SignCounts total = new SignCounts();
        for (SignCounts part : perThread)
            total.addAll(part);

        Report.print("task1a", "-", threadCount, elapsed, total, cli);
    }

    private static SignCounts countRange(Path path, long start, long end) throws Exception {
        SignCounts counts = new SignCounts();
        NumberParser parser = new NumberParser();
        byte[] buffer = new byte[CHUNK_SIZE];
        ByteBuffer wrapper = ByteBuffer.wrap(buffer);

        try (FileChannel file = FileChannel.open(path, StandardOpenOption.READ)) {
            // Start one byte early: if that byte is not '\n', the first line belongs to the previous thread and is skipped.
            long position = start > 0 ? start - 1 : 0;
            boolean skipping = start > 0;

            while (true) {
                long remaining = end - position;
                int toRead = remaining > 0 ? (int) Math.min(CHUNK_SIZE, remaining) : 64;

                wrapper.clear().limit(toRead);
                int bytesRead = file.read(wrapper, position);   // a read at an explicit offset, like RandomAccess.Read

                if (bytesRead <= 0) {
                    parser.finishLine(counts);
                    return counts;
                }

                for (int i = 0; i < bytesRead; i++) {
                    byte b = buffer[i];
                    long bytePosition = position + i;

                    if (skipping) {
                        if (b == '\n') {
                            skipping = false;
                            if (bytePosition + 1 >= end) return counts;
                        }
                        continue;
                    }

                    // A line just ended; if the next one starts at or after `end`, it belongs to the next thread.
                    if (parser.feed(b, counts) && bytePosition + 1 >= end)
                        return counts;
                }

                position += bytesRead;
            }
        }
    }
}
