# Lab 1: Concurrency in Practice (C# / .NET 10)

One console program; every task and configuration is selected with command-line parameters.

## Files

| File | What it does |
|---|---|
| `Program.cs` | Entry point: reads the first word (`gen`, `task1a`, ...) and runs that task |
| `DataGenerator.cs` | Creates `data/numbers.txt`, the 8 part files and the expected totals |
| `Task1a.cs` | 1a: file split into N byte ranges, one thread per range |
| `Task1b.cs` | 1b: shared counters without sync / with a lock / per-thread counters |
| `Task1c.cs` | 1c: one reader thread + N workers connected by a bounded blocking queue |
| `Task2.cs` | 2: 8 files on the USB drive, each read in one operation into one byte buffer |
| `NumberParser.cs` | Turns raw bytes (`-42\n7\n`) into numbers (used by 1a and 2) |
| `SignCounts.cs` | The three counters (negative / positive / zero) |
| `ThreadRunner.cs` | Starts N OS threads and waits for all; splits a range into N equal parts |
| `Report.cs` | Prints time + peak memory, checks the totals, appends to a CSV |
| `run_task1.ps1` / `run_task2.ps1` | Run every configuration and save the results to `results/*.csv` |

## Build and run

Requirements: the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`).

Run these from the `Lab1` folder:

```powershell
dotnet build -c Release                      # compile (Release = optimised)
$exe = "..\artifacts\bin\Lab1\release\Lab1.exe"

& $exe gen                                   # 1) create the data (~3 s, ~700 MB on disk)
& $exe task1a --threads 8                    # Task 1a with 8 threads
& $exe task1b --version all                  # Task 1b: v1 x3, v2, v3 with 32 threads
& $exe task1c --workers 4                    # Task 1c with 4 workers
& $exe task2  --threads 2 --dir E:\          # Task 2 reading from the USB drive E:
& $exe info                                  # CPU / OS / runtime info
& $exe help                                  # all options
```

`dotnet run -c Release -- task1a --threads 8` also works (it builds first). Everything after `--` goes to our program.

**Where the .exe is:** inside the course folder, `RC_E_F\Directory.Build.props` sends all build output to
`RC_E_F\artifacts\`, so the Lab1 folder holds only source code. If the Lab1 folder is built on its own, the program is
at `bin\Release\net10.0\Lab1.exe` instead. The run scripts find it in either place.

Every task prints the time, the peak memory, the three totals and **CORRECT/WRONG**. The totals are compared with
`data/numbers.expected.txt`, which the generator writes.

### All Task 1 configurations at once

```powershell
powershell -ExecutionPolicy Bypass -File .\run_task1.ps1          # each 1a/1c config 3 times
powershell -ExecutionPolicy Bypass -File .\run_task1.ps1 -Runs 5
```

### Task 2 (USB flash drive)

```powershell
# first time: copies data\parts\numbers_*.txt to the drive, then runs 1, 2, 4 threads
powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Dir E:\ -Copy
```

The script stops before every run and asks you to **eject, unplug and re-plug** the drive. This way Windows cannot
serve the files from its memory cache.

## Hardware and software

| | |
|---|---|
| CPU | AMD Ryzen 9 9955HX |
| Physical cores / logical processors | 16 / 32 (SMT: 2 hardware threads per core) |
| RAM | 32 GB (31.2 GB usable) |
| Disk | Micron MTFDKBA1T0QGN, 1 TB NVMe SSD |
| USB drive | *TODO: model, capacity, USB 2.0 / 3.x* |
| OS | Windows 11 Pro (10.0.26200) |
| Language / runtime | C# 14, .NET SDK 10.0.401, runtime .NET 10.0.12 (x64) |

## Data

- `numbers.txt`: 50,000,000 integers in [-1,000,000, 1,000,000], seed 12345, `\n` line endings, 352 MB
- `parts/numbers_1..8.txt`: the same numbers, in order, 6,250,000 per file (~44 MB each)
- Correct totals: negative = 25,007,776, positive = 24,992,202, zero = 22 (total 50,000,000)
