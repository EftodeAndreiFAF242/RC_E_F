using System.Diagnostics;
using System.Runtime.InteropServices;
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
/// Each thread also measures where its time goes:
///   I/O time = waiting for the drive to deliver the bytes (opening + reading the file)
///   CPU time = parsing the bytes and counting the numbers
/// If I/O time is much bigger than CPU time, the work is I/O-bound; if it's the other way
/// round, it's CPU-bound. These two numbers answer the main question of Task 2.
///
/// IMPORTANT (from the lab): before every run, eject the USB drive, unplug it and plug it
/// back in, so Windows cannot serve the files from its memory cache (run_task2.ps1 does this).
///
/// Extra option, NOT required by the lab:  --cache off
///   opens the files with FILE_FLAG_NO_BUFFERING, which tells Windows "do not use the file
///   cache for this file, read straight from the device". It gives "cold" reads without
///   re-plugging, so it can be repeated, and it also works on the internal SSD.
/// </summary>
public static class Task2
{
    // FILE_FLAG_NO_BUFFERING from the Windows API. .NET has no named FileOptions value for it,
    // but it accepts this number and passes it on to Windows.
    private const FileOptions NoBuffering = (FileOptions)0x20000000;

    // Without the cache, Windows reads only WHOLE SECTORS straight into our memory, so the
    // buffer address and the number of bytes we ask for must be multiples of the sector size.
    // 4096 bytes is a multiple of every common sector size (512 and 4096).
    private const int SectorSize = 4096;

    public static void Run(CommandLine cli)
    {
        string dir = cli.GetString("--dir", Paths.PartsDir);   // e.g. --dir E:\lab1_task2 (the USB drive)
        int threadCount = cli.GetInt("--threads", 1);
        string cache = cli.GetString("--cache", "on");
        if (cache != "on" && cache != "off")
            throw new ArgumentException("--cache must be 'on' or 'off'.");
        bool useCache = cache == "on";

        // --label only changes the name written in the output / CSV (e.g. "cold" or "warm"),
        // so the run script can say whether the drive was just re-plugged.
        string label = cli.GetString("--label", useCache ? "-" : "nocache");

        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Folder '{dir}' not found.");

        // Find numbers_1.txt ... numbers_8.txt and sort them so the order is always the same.
        string[] files = Directory.GetFiles(dir, "numbers_*.txt");
        Array.Sort(files);
        if (files.Length == 0)
            throw new FileNotFoundException($"No numbers_*.txt files in '{dir}'. Copy data\\parts\\* there first.");

        Console.WriteLine($"{files.Length} files in {Path.GetFullPath(dir)}, cache {cache}");

        var perThread = new SignCounts[threadCount];
        var ioTicks   = new long[threadCount];   // per thread: Stopwatch ticks spent opening + reading
        var cpuTicks  = new long[threadCount];   // per thread: Stopwatch ticks spent parsing
        var bytesRead = new long[threadCount];   // per thread: how many bytes it read

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
            byte[] buffer = AllocateBuffer((int)largest, useCache, out int start);

            for (long i = from; i < to; i++)
            {
                // Stopwatch.GetTimestamp() = the current value of a very precise clock, in "ticks".
                long t0 = Stopwatch.GetTimestamp();

                // `using` inside the loop: the file is closed at the end of each iteration.
                using SafeFileHandle file = File.OpenHandle(files[i], FileMode.Open, FileAccess.Read,
                                                            FileShare.Read,
                                                            useCache ? FileOptions.None : NoBuffering);
                int length = (int)RandomAccess.GetLength(file);   // this file's size in bytes (~44 MB)

                // With the cache on we ask for exactly `length` bytes. With the cache off Windows
                // wants a whole number of sectors, so we ask for `length` rounded UP to a sector;
                // Windows still returns only the `length` bytes that exist.
                int request = useCache ? length : RoundUpToSector(length);

                // ONE read operation into ONE byte buffer: Windows returns the whole file in a
                // single call. (The loop is only a safety net: the OS is allowed to return fewer
                // bytes than asked, and then we'd ask for the rest.)
                int filled = 0;
                while (filled < length)
                {
                    int n = RandomAccess.Read(file, buffer.AsSpan(start + filled, request - filled), filled);
                    if (n == 0) break;                            // file got shorter? stop
                    filled += n;
                }

                long t1 = Stopwatch.GetTimestamp();               // the bytes are now in memory

                // Parse the bytes. `+=` works because we defined operator + on SignCounts.
                local += NumberParser.CountAll(buffer.AsSpan(start, filled));

                long t2 = Stopwatch.GetTimestamp();               // parsing finished

                ioTicks[t]   += t1 - t0;   // each thread writes only its OWN slot: no race
                cpuTicks[t]  += t2 - t1;
                bytesRead[t] += filled;
            }

            perThread[t] = local;
        });

        stopwatch.Stop();

        var total = new SignCounts();
        foreach (SignCounts part in perThread)
            total += part;

        // Stopwatch.Frequency = ticks per second, so ticks * 1000 / Frequency = milliseconds.
        double ioMs  = ioTicks.Sum()  * 1000.0 / Stopwatch.Frequency;
        double cpuMs = cpuTicks.Sum() * 1000.0 / Stopwatch.Frequency;
        double megabytes = bytesRead.Sum() / (1024.0 * 1024.0);

        Report.Print("task2", label, threadCount, stopwatch.Elapsed, total, cli, ioMs, cpuMs);
        Console.WriteLine($"        I/O (open + read) = {ioMs,9:F1} ms   CPU (parse) = {cpuMs,8:F1} ms   " +
                          $"(sums over all threads; I/O share = {100 * ioMs / (ioMs + cpuMs):F1} %)");
        Console.WriteLine($"        {megabytes:F1} MB read -> {megabytes / stopwatch.Elapsed.TotalSeconds:F1} MB/s overall");
    }

    /// <summary>
    /// Creates this thread's buffer. With the cache on, a normal array is enough (start = 0).
    /// With the cache off, the data must land at a memory address that is a multiple of
    /// <see cref="SectorSize"/>, so we allocate a bit more and start at the first aligned byte.
    /// </summary>
    private static byte[] AllocateBuffer(int size, bool useCache, out int start)
    {
        start = 0;
        if (useCache)
            return new byte[size];

        // pinned: true = the garbage collector will never MOVE this array, so its address
        // stays valid while Windows writes into it. (Normally .NET may move objects around.)
        byte[] array = GC.AllocateUninitializedArray<byte>(RoundUpToSector(size) + SectorSize, pinned: true);

        // The address of the first element, as a number. % = remainder of the division.
        long address = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
        start = (int)((SectorSize - address % SectorSize) % SectorSize);   // bytes to skip to reach alignment
        return array;
    }

    // Example: 10,000 -> 12,288 (= 3 x 4096). Integer division drops the remainder.
    private static int RoundUpToSector(int n) => (n + SectorSize - 1) / SectorSize * SectorSize;
}
