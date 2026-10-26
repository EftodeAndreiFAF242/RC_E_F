package lab1;

/** Helpers shared by all tasks for creating threads and dividing work (same as ThreadRunner.cs). */
final class ThreadRunner {
    private ThreadRunner() { }

    /**
     * The work one thread does. It is like C#'s Action&lt;int&gt;, but it may throw checked
     * exceptions (IOException, InterruptedException...): Java's own Runnable may not.
     */
    @FunctionalInterface
    interface Body {
        void run(int index) throws Exception;
    }

    /**
     * Creates `count` real operating-system threads; thread number t runs body.run(t).
     * Returns only after ALL threads have finished. If a thread failed, its error is re-thrown here.
     */
    static void runAll(int count, Body body) throws Exception {
        if (count < 1) throw new IllegalArgumentException("The number of threads must be at least 1.");

        Thread[] threads = new Thread[count];
        Exception[] errors = new Exception[count];   // an exception inside a thread would otherwise be lost

        for (int t = 0; t < count; t++) {
            // A Java lambda may only use local variables that never change ("effectively final").
            // `t` changes (t++), so writing body.run(t) inside the lambda would NOT compile.
            // (C# compiles it and silently shares the variable, which is a classic bug.)
            final int index = t;

            // new Thread(...) creates a PLATFORM thread = one real OS thread.
            // (Java 21+ also has "virtual threads", which are NOT OS threads; the lab wants OS threads.)
            threads[t] = new Thread(() -> {
                try {
                    body.run(index);
                } catch (Exception e) {
                    errors[index] = e;
                }
            });
            threads[t].start();   // now the OS schedules it on some core
        }

        for (Thread thread : threads)
            thread.join();        // wait until that thread has finished

        for (Exception e : errors)
            if (e != null) throw e;
    }

    /**
     * Divides [0, length) into `parts` nearly equal pieces and returns piece `index` as {start, end}.
     * Java has no tuples, so a two-element array is returned instead of C#'s (Start, End).
     */
    static long[] split(long length, int index, int parts) {
        return new long[] { length * index / parts, length * (index + 1) / parts };
    }
}
