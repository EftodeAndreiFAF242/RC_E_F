using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Lab1;

/// <summary>
/// Task 2: many files on slow storage (the USB flash drive).
///
/// A FILE is the unit of work. The 8 files are divided among the threads as evenly as
/// possible (1 thread -> all 8 files, 2 threads -> 4 files each, 4 threads -> 2 files each).
/// For each file, a thread reads it with ONE call into ONE byte array (not as lines),
/// then parses that array with the same NumberParser used in Task 1a.
///
/// IMPORTANT (from the lab): before every run, eject the USB drive, unplug it and plug it
/// back in, so Windows cannot serve the files from its memory cache.
/// </summary>
public static class Task2
{
    public static void Run(CommandLine cli)
    {
        string dir = cli.GetString("--dir", Paths.PartsDir);   // e.g. --dir E:\  (the USB drive)
        int threadCount = cli.GetInt("--threads", 1);

        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Folder '{dir}' not found.");

        // Find numbers_1.txt ... numbers_8.txt and sort them so the order is always the same.
        string[] files = Directory.GetFiles(dir, "numbers_*.txt");
        Array.Sort(files);
        if (files.Length == 0)
            throw new FileNotFoundException($"No numbers_*.txt files in '{dir}'. Copy data\\parts\\* there first.");

        Console.WriteLine($"{files.Length} files in {Path.GetFullPath(dir)}");

        var perThread = new SignCounts[threadCount];
        var stopwatch = Stopwatch.StartNew();

        ThreadRunner.RunAll(threadCount, t =>
        {
            // Thread t gets a consecutive group of files, e.g. with 2 threads: files 0-3 and 4-7.
            var (from, to) = ThreadRunner.Split(files.Length, t, threadCount);
            var local = new SignCounts();

            // This thread's byte buffer, REUSED for each of its files.
            // Why not simply File.ReadAllBytes(path)? It creates a new ~44 MB array for every
            // file. Arrays that big live on .NET's "Large Object Heap", which is cleaned up only
            // rarely, so all 8 old buffers stay in memory and the peak memory would be ~380 MB
            // no matter how many threads we use. With one reused buffer per thread, the peak
            // memory really shows "threads x file size".
            // The files have the same number of LINES but slightly different sizes in BYTES, so
            // we size the buffer for the largest of this thread's files, once, before the loop.
            long largest = 0;
            for (long i = from; i < to; i++)
                largest = Math.Max(largest, new FileInfo(files[i]).Length);
            byte[] buffer = new byte[largest];

            for (long i = from; i < to; i++)
            {
                // `using` inside the loop: the file is closed at the end of each iteration.
                using SafeFileHandle file = File.OpenHandle(files[i], FileMode.Open, FileAccess.Read);
                int length = (int)RandomAccess.GetLength(file);   // this file's size in bytes (~44 MB)

                // ONE read operation into ONE byte buffer: Windows returns the whole file in a
                // single call. (The loop is only a safety net: the OS is allowed to return fewer
                // bytes than asked, and then we'd ask for the rest.)
                int filled = 0;
                while (filled < length)
                {
                    int n = RandomAccess.Read(file, buffer.AsSpan(filled, length - filled), filled);
                    if (n == 0) break;                            // file got shorter? stop
                    filled += n;
                }

                // Parse the bytes. `+=` works because we defined operator + on SignCounts.
                local += NumberParser.CountAll(buffer.AsSpan(0, filled));
            }

            perThread[t] = local;
        });

        stopwatch.Stop();

        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;

        Report.Print("task2", "-", threadCount, stopwatch.Elapsed, total, cli);
    }
}
