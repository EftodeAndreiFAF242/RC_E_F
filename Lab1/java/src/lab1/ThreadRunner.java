package lab1;

/** Starts N real OS threads and waits for all of them, and splits a range of work into N equal parts. */
final class ThreadRunner {
    private ThreadRunner() { }

    /** Like C#'s Action<int>, but allowed to throw checked exceptions such as IOException. */
    @FunctionalInterface
    interface Body {
        void run(int index) throws Exception;
    }

    static void runAll(int count, Body body) throws Exception {
        if (count < 1) throw new IllegalArgumentException("The number of threads must be at least 1.");

        Thread[] threads = new Thread[count];
        Exception[] errors = new Exception[count];   // an exception inside a thread would otherwise be lost

        for (int t = 0; t < count; t++) {
            final int index = t;   // a lambda may only use variables that never change
            threads[t] = new Thread(() -> {
                try {
                    body.run(index);
                } catch (Exception e) {
                    errors[index] = e;
                }
            });
            threads[t].start();
        }

        for (Thread thread : threads)
            thread.join();

        for (Exception e : errors)
            if (e != null) throw e;
    }

    static long[] split(long length, int index, int parts) {
        return new long[] { length * index / parts, length * (index + 1) / parts };
    }
}
