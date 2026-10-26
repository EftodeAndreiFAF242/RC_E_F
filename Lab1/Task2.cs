using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Lab1;

/// <summary>
/// Task 2: each thread reads its share of the 8 files, every file with one read into one byte buffer, and measures
/// how long it waited for the drive (I/O) and how long it parsed (CPU) to show which one limits the work.
/// </summary>
public static class Task2
{
    // FILE_FLAG_NO_BUFFERING: Windows reads straight from the device instead of the file cache (--cache off).
    private const FileOptions NoBuffering = (FileOptions)0x20000000;

    // Without the cache the buffer address and the read size must be multiples of the sector size.
    private const int SectorSize = 4096;

    public static void Run(CommandLine cli)
    {
        string dir = cli.GetString("--dir", Paths.PartsDir);
        int threadCount = cli.GetInt("--threads", 1);
        string cache = cli.GetString("--cache", "on");
        if (cache != "on" && cache != "off")
            throw new ArgumentException("--cache must be 'on' or 'off'.");
        bool useCache = cache == "on";
        string label = cli.GetString("--label", useCache ? "-" : "nocache");

        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Folder '{dir}' not found.");

        string[] files = Directory.GetFiles(dir, "numbers_*.txt");
        Array.Sort(files);
        if (files.Length == 0)
            throw new FileNotFoundException($"No numbers_*.txt files in '{dir}'. Copy data\\parts\\* there first.");

        Console.WriteLine($"{files.Length} files in {Path.GetFullPath(dir)}, cache {cache}");

        var perThread = new SignCounts[threadCount];
        var ioTicks   = new long[threadCount];
        var cpuTicks  = new long[threadCount];
        var bytesRead = new long[threadCount];

        var stopwatch = Stopwatch.StartNew();

        ThreadRunner.RunAll(threadCount, t =>
        {
            var (from, to) = ThreadRunner.Split(files.Length, t, threadCount);
            var local = new SignCounts();

            // One buffer per thread, reused for all its files, so the peak memory shows threads x file size.
            long largest = 0;
            for (long i = from; i < to; i++)
                largest = Math.Max(largest, new FileInfo(files[i]).Length);
            byte[] buffer = AllocateBuffer((int)largest, useCache, out int start);

            for (long i = from; i < to; i++)
            {
                long t0 = Stopwatch.GetTimestamp();

                using SafeFileHandle file = File.OpenHandle(files[i], FileMode.Open, FileAccess.Read,
                                                            FileShare.Read,
                                                            useCache ? FileOptions.None : NoBuffering);
                int length = (int)RandomAccess.GetLength(file);
                int request = useCache ? length : RoundUpToSector(length);

                // One read for the whole file; the loop only continues if the OS returns fewer bytes than asked.
                int filled = 0;
                while (filled < length)
                {
                    int n = RandomAccess.Read(file, buffer.AsSpan(start + filled, request - filled), filled);
                    if (n == 0) break;
                    filled += n;
                }

                long t1 = Stopwatch.GetTimestamp();

                local += NumberParser.CountAll(buffer.AsSpan(start, filled));

                long t2 = Stopwatch.GetTimestamp();

                ioTicks[t]   += t1 - t0;
                cpuTicks[t]  += t2 - t1;
                bytesRead[t] += filled;
            }

            perThread[t] = local;
        });

        stopwatch.Stop();

        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;

        double ioMs  = ioTicks.Sum()  * 1000.0 / Stopwatch.Frequency;
        double cpuMs = cpuTicks.Sum() * 1000.0 / Stopwatch.Frequency;
        double megabytes = bytesRead.Sum() / (1024.0 * 1024.0);

        Report.Print("task2", label, threadCount, stopwatch.Elapsed, total, cli, ioMs, cpuMs);
        Console.WriteLine($"        I/O (open + read) = {ioMs,9:F1} ms   CPU (parse) = {cpuMs,8:F1} ms   " +
                          $"(sums over all threads; I/O share = {100 * ioMs / (ioMs + cpuMs):F1} %)");
        Console.WriteLine($"        {megabytes:F1} MB read -> {megabytes / stopwatch.Elapsed.TotalSeconds:F1} MB/s overall");
    }

    // With the cache off, the data must start at a sector-aligned address in a pinned array the GC never moves.
    private static byte[] AllocateBuffer(int size, bool useCache, out int start)
    {
        start = 0;
        if (useCache)
            return new byte[size];

        byte[] array = GC.AllocateUninitializedArray<byte>(RoundUpToSector(size) + SectorSize, pinned: true);
        long address = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
        start = (int)((SectorSize - address % SectorSize) % SectorSize);
        return array;
    }

    private static int RoundUpToSector(int n) => (n + SectorSize - 1) / SectorSize * SectorSize;
}
