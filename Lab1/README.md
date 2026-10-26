# Lab 1: Concurrency in Practice (C# / .NET 10 + Java 25)

Network Programming, Technical University of Moldova. Student: **Eftode Andrei, FAF-242**. Teacher: **Cristofor Fistic**.

The program counts how many of 50,000,000 numbers are negative, positive and zero, using 1–64 threads in different ways.
The point is to **measure** when threads help and when they don't. It is one console program: every task and
configuration is selected with command-line parameters. It is written twice, in **C#** (the main version) and in
**Java** (the bonus: comparing two languages).

- The Word report: [`report/Lab1_Report_Eftode_Andrei_FAF-242.docx`](report/Lab1_Report_Eftode_Andrei_FAF-242.docx)
- All measurements: [`results/`](results/) (raw CSV files) and [`results/summary.md`](results/summary.md) (median tables)

This README is also a **study guide**. It explains every idea, every file and every result, so you can answer any
question at the defense. Read it top to bottom once, then use the table of contents.

## Contents

1. [Quick start: build and run](#1-quick-start-build-and-run)
2. [What the lab asks](#2-what-the-lab-asks)
3. [What happened in this lab, step by step](#3-what-happened-in-this-lab-step-by-step)
4. [The theory you need](#4-the-theory-you-need)
5. [The data](#5-the-data)
6. [Map of the files](#6-map-of-the-files)
7. [Code walkthrough](#7-code-walkthrough)
8. [Results and what they mean](#8-results-and-what-they-mean)
9. [Answers to the report questions (short)](#9-answers-to-the-report-questions-short)
10. [Control questions: answers for the defense](#10-control-questions-answers-for-the-defense)
11. [Defense cheat-sheet: questions about the code](#11-defense-cheat-sheet-questions-about-the-code)
12. [C# vs Java (the bonus)](#12-c-vs-java-the-bonus)
13. [Glossary](#13-glossary)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Quick start: build and run

**Requirements**

| What | Needed for | Install |
|---|---|---|
| .NET 10 SDK | the C# program (all tasks) | `winget install Microsoft.DotNet.SDK.10` |
| JDK 22 or newer (Temurin 25 used) | the Java version (bonus) | `winget install EclipseAdoptium.Temurin.25.JDK` |
| Node.js 18+ | only to rebuild the Word report | `winget install OpenJS.NodeJS.LTS` |

Everything below runs from the **`Lab1` folder** in PowerShell.

```powershell
# ---- C# ----
dotnet build -c Release                          # compile (Release = optimised)
$exe = "..\artifacts\bin\Lab1\release\Lab1.exe"  # where the build puts the program (see below)

& $exe gen                                       # 1) create the data (~3 s, ~700 MB): numbers.txt, 8 parts, expected totals
& $exe task1a --threads 8                        # Task 1a with 8 threads
& $exe task1b --version all                      # Task 1b: v1 three times, then v2 and v3 (32 threads by default)
& $exe task1c --workers 4                        # Task 1c: 1 reader + 4 workers
& $exe task1c --workers 4 --batch 1              #   question 6: batches of one line
& $exe task1c --workers 1 --capacity 0           #   question 6: queue without a size limit
& $exe task2  --threads 2 --dir D:\lab1_task2    # Task 2 reading the 8 files from the USB drive D:
& $exe task2  --threads 2 --cache off            # Task 2 on the SSD, bypassing the Windows file cache
& $exe info                                      # CPU / OS / .NET information
& $exe help                                      # all commands and options

# ---- Java (same commands, same options; the data comes from the C# "gen") ----
javac -d java/out -encoding UTF-8 java/src/lab1/*.java
java --enable-native-access=ALL-UNNAMED -cp java/out lab1.Main task1a --threads 8
```

Every run prints the time, the peak memory, the three totals and **CORRECT** or **WRONG** (compared with
`data/numbers.expected.txt`). Add `--csv results\my.csv` to any command to append the result to a CSV file.

**Where the .exe is.** `RC_E_F\Directory.Build.props` sends all build output to `RC_E_F\artifacts\`, so the Lab1 folder
holds only source code. If the Lab1 folder is built on its own, the program is at `bin\Release\net10.0\Lab1.exe`.
The scripts find it in either place. In Visual Studio, open `RC_E_F.slnx` and pick a launch profile (`task1a`, `task2`, ...)
from `Properties/launchSettings.json`.

**Run everything (what produced the results):**

```powershell
# Task 1: C# and Java, every configuration 5 times, plus the question-6 experiments (~12 min)
powershell -ExecutionPolicy Bypass -File .\run_task1.ps1 -Lang both -Runs 5 -Extras

# Task 2: first time copies the 8 files to the USB drive; then before EVERY run it ejects the drive,
# waits for you to pull it out and plug it back in, and measures (beep + pop-up tell you when).
powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Copy
powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Lang both -Warm
#   -StartAt N : resume a session at run N    -Threads 1,2,4 : thread counts    -Manual : eject yourself

# The Word report and results/summary.md from all CSV files
cd report; npm install; node build_report.mjs
```

---

## 2. What the lab asks

From *Lab 1 – Concurrency in practice.pdf*:

- **Data:** `numbers.txt` with 50,000,000 random integers from −1,000,000 to 1,000,000, one per line, and the same numbers
  split in order into 8 files of 6,250,000. In every task the negative + positive + zero counts must add up to 50,000,000
  and be the same in all tasks.
- **Task 1a:** count with 1, 2, 4, 8, 16, 32, 64 threads; the file is divided into as many parts as threads; each thread reads
  and parses only its own part; no line counted twice or skipped. Measure time and peak memory.
- **Task 1b:** numbers loaded into an `int[]`; with as many threads as logical processors count in three ways: shared
  counters without synchronisation, shared counters with a lock, one counter per thread added at the end. Run version 1 three times.
- **Task 1c:** one reader thread reads line by line, puts batches of 10,000 lines into a thread-safe queue of at most 100
  batches; worker threads take batches and count; the reader closes the queue at the end; 1–64 workers.
- **Task 2:** the 8 files on a **USB flash drive**; before every run eject, unplug and re-plug the drive (no file cache);
  one thread reads one whole file in a single operation into one byte buffer; files divided evenly; 1, 2, 4 threads.
- **Report:** repository link, hardware, for every task charts of time / speed-up / peak memory + answers, conclusion.
- **Bonus:** the same tasks in another language, compared.

| Grade | Needed |
|---:|---|
| 5 | Task 1a |
| 6 | Task 1b |
| 7 | Task 1c |
| 8 | Task 2 |
| 9 | at least half of the control questions answered at the defense |
| 10 | all control questions answered at the defense |

A task counts only if its totals are correct, its questions are answered in the report, and **you can explain its code**.
That's why sections 7, 10 and 11 exist.

---

## 3. What happened in this lab, step by step

### Session 1 (29 September 2026): the C# program and Task 1

- Wrote the C# program (this folder): data generator, Tasks 1a, 1b, 1c, 2, helpers, run scripts, a first README.
- Generated the data (seed 12345) and measured Task 1 once per configuration
  (kept in [`results/2026-09-29_first_session/`](results/2026-09-29_first_session/)).
- Task 2 could not be measured: **no USB flash drive yet**.

### Session 2 (26 October 2026): the USB drive, Java, the report

1. **Checked the USB drive.** It appeared as `D:`, 124 GB, "VendorCo ProductCode USB Device". At first it held a Windows
   installer (made with Microsoft's Media Creation Tool, label `ESD-USB`, only a 32 GB FAT32 partition). Then you formatted
   it, so it held only an empty 32 GB FAT32 partition.
   - Its USB controller IDs are **VID 048D / PID 1234**, a generic chip often found in no-name and *fake-capacity* sticks.
     Nothing outside the first 32 GB was used, and every file was checked byte by byte (see step 5).
   - **USB version:** I asked the USB hub driver directly (the same data the USBView tool shows). The device says
     `bcdUSB = 2.00` and the negotiated speed is **High-Speed = USB 2.0 (480 Mbit/s)**, although the port is USB 3.
     It is a USB 2.0 stick.
2. **Reviewed the code.** Tasks 1a/1b/1c were correct and well commented. Added:
   - **Task 2 time split:** every thread measures how long it waits for the drive (open + read = *I/O*) and how long it
     parses (*CPU*). This answers "CPU-bound or I/O-bound?" with numbers.
   - **`--cache off`** for Task 2: reads with `FILE_FLAG_NO_BUFFERING` (bypasses the Windows file cache) - an *extra*
     experiment; the official runs still use the replug.
   - **CSV columns** `lang`, `io_ms`, `cpu_ms`, `queue_max`, so C# and Java results go in the same files.
   - **`run_task2.ps1` rewritten**: finds the drive by its folder (the letter may change), **ejects it from code**
     (Windows `CM_Request_Device_Eject` = "Safely Remove Hardware"), waits until you pull it out and plug it back in,
     waits until it has stayed connected for 8 s, runs, beeps and shows a pop-up for the next replug.
3. **Installed Java** (Eclipse Temurin 25 JDK) and wrote the **Java version** (`java/src/lab1/`), same commands and output.
4. **Copied the 8 files** to `D:\lab1_task2` (the rest of the drive was not touched): 352 MB in 78 s = **4.5 MB/s write**.
   Then read them back with `--cache off`: **CORRECT**, 30.2 s, **11.7 MB/s read**, 98.9 % of the time waiting for the drive.
5. **The replug session (Task 2).** For every configuration: eject → you unplug → you re-plug → measure.
   - C# 1 thread: **30.3 s** (then from the cache, without replug: **0.35 s** - 87× faster: this is why the lab demands replugging).
   - C# 2 threads: **30.0 s**, C# 4 threads: **29.9 s**. Java 1 thread: **30.5 s**. More threads did not help at all.
   - **Problem:** the stick disconnected in the middle of some runs ("A device which does not exist was specified").
     The Windows System log showed **1,341 read errors** ("disk", event 51) from this stick. Windows retries failed reads, but
     sometimes the stick resets itself and disappears. It happened **only with 2+ threads reading at once** (5 failed runs:
     1 C#, 4 Java attempts with 2 threads).
   - **Fixes in the script:** a run that fails is repeated after another replug; `-StartAt` resumes an interrupted session;
     measuring starts only after the drive has been connected for 8 s; the pop-ups say clearly when *not* to touch it.
   - **Result:** the required part (C#, 1/2/4 threads) is complete and correct. The Java runs with 2 and 4 threads on the USB
     could not be completed with this stick (bonus only). Every run that finished reported the exact correct totals, so the
     data read from the stick was right. (The script also compares SHA-256 hashes at the end of a session; our sessions were
     stopped before that step because of the disconnects.)
6. **Task 1 measured again**, C# and Java, every configuration **5 times** (the report uses the **median**).
7. Found while analysing: Java Task 1a is fastest with 8 threads and gets slower after. The JVM compilation log
   (`-XX:+PrintCompilation`) explained it: JIT warm-up and deoptimisation (section 12).
8. **Added a queue measurement to Task 1c** (`queue_max`: the most batches ever waiting) and measured 1c again. It shows who
   the bottleneck is (section 8.3).
9. **Extra Task 2 experiment on the internal SSD** (`--cache off`, 1/2/4/8 threads): the same program is CPU-bound there.
10. **The Word report** is generated from the CSV files by `report/build_report.mjs` (charts drawn as SVG, converted to PNG,
    document written with the `docx` library), then checked page by page in Word.

---

## 4. The theory you need

### 4.1 Why we need threads at all (lecture: "the free lunch")

From ~1975 to 2004 clock speeds rose from MHz to ~4 GHz, so old single-threaded programs got faster for free. Around 2004 the
heat became impossible to remove, and clocks stopped at about 4–5 GHz. Since then processors get **more cores** instead of faster
ones. To use them, a program must do several things at the same time: it must be **concurrent**.

### 4.2 Physical core vs logical processor (hyper-threading / SMT)

- A **physical core** is a complete computing unit: its own arithmetic units, registers and L1/L2 cache.
  My Ryzen 9 9955HX has **16**.
- **SMT** (Simultaneous Multi-Threading; Intel calls it Hyper-Threading) gives every core **two hardware threads** = two
  **logical processors**. The OS sees **32** processors. The two hardware threads *share* the core's execution units: when one
  waits (for example for data from memory), the other one uses the core. The lecture's analogy: one worker with two inboxes.
  The gain is typically 15–30 %, not 2×.
- **Where it showed:** Task 1a in C# got 10.2× faster with 16 threads (one per physical core), and only 11.3× with
  32 (+11 % from SMT). With 64 it got slower. The charts have lines at 16 and 32.

### 4.3 Concurrency vs parallelism

- **Concurrency** = *dealing with* many things at once: several tasks are in progress, possibly by switching between them on
  one core (lecture: one cook with pasta, bread and salad; two queues, one soda machine).
- **Parallelism** = *doing* many things at once: tasks really run at the same instant on different cores (two soda machines).
- The four combinations: not concurrent & not parallel (one task after another), concurrent but not parallel (one core
  switching), parallel but not concurrent (two cores, one task each), concurrent and parallel (several cores, each switching).
- **Multithreading** = concurrency with several threads. **Parallel processing** = splitting work among threads that run
  at once (this lab). **Asynchronous programming** = concurrency *without* blocking threads (futures/callbacks, `async/await`;
  later lectures).

### 4.4 Program, process, thread

- **Program**: instructions in a file on disk; nothing runs.
- **Process**: a running program with its own memory (code, data, heap), open files/sockets (handles), and at least one thread.
- **Thread**: one flow of execution inside a process: its own **program counter, registers and stack**. Threads of one process
  **share** the heap, static fields and handles. They do **not** share stacks, so **local variables belong to one thread**.
  That is exactly why "per-thread counters" (Task 1b v3) are safe: a local variable is private by construction.

### 4.5 What a thread costs (lecture)

A thread costs about **1 MB of reserved stack** (address space; only the pages actually used take RAM), a kernel object, a
thread environment block, a slot in the scheduler, and **tens of microseconds** to create. 10,000 threads would be 10 GB of
address space. So threads are not free: more threads than cores only adds cost. You can see it in Task 1a with 64 threads.

### 4.6 CPU-bound vs I/O-bound

- **CPU-bound**: the speed is limited by how fast the processor computes. The core is busy ~100 %. More cores → faster,
  up to the number of cores (lecture: peeling 100 potatoes; more people finish sooner).
- **I/O-bound**: the speed is limited by waiting for something outside the processor: disk, USB, network, database. The core
  is mostly idle (lecture: waiting for a pizza; more people → still 30 minutes).
- **The test:** *would more workers finish it sooner?* Yes → CPU-bound. No → you are only waiting.
- In this lab: parsing numbers from RAM = CPU-bound; reading from a USB 2.0 stick = I/O-bound.

### 4.7 Speed-up and Amdahl's law

- **Speed-up** S(n) = T(1) / T(n). **Ideal** = n (n threads → n times faster).
- **Amdahl's law:** if a fraction *s* of the work is serial (can't be split), then S(n) ≤ 1 / (s + (1 − s)/n), and never more
  than 1/s. Example: if 10 % is serial, even infinite threads give at most 10×.
- Where we met it: Task 1c (the single reader thread is serial: 1c never beats ~1 s); Java's JIT warm-up (a fixed cost);
  thread creation (a cost per thread).

### 4.8 Race condition and lost updates (control question 3)

`counter++` looks like one operation, but the CPU does **three**: (1) **read** the value from memory into a register,
(2) **add** 1 in the register, (3) **write** the register back. With two threads A and B and `counter = 100`:

| step | thread A | thread B | counter in memory |
|---|---|---|---|
| 1 | reads 100 | | 100 |
| 2 | | reads 100 | 100 |
| 3 | adds → 101 | | 100 |
| 4 | | adds → 101 | 100 |
| 5 | writes 101 | | 101 |
| 6 | | writes 101 | **101** (should be 102) |

Two numbers were counted, but the counter grew by 1: **one update is lost**. A **race condition** = the result depends on the
timing of threads. With 32 threads hammering three counters, **88–95 %** of the updates were lost.

### 4.9 Locks (mutexes) and contention

A **lock** (mutex = mutual exclusion) lets only one thread at a time execute the protected code. `lock (gate) { counter++; }`
makes read-add-write indivisible *with respect to the other threads using the same lock* → correct. But:
- taking and releasing a lock costs time (atomic instructions, memory barriers);
- if 32 threads want the same lock (**contention**), all but one **wait** (first spinning, then sleeping in the OS and
  being woken up again). The work becomes serial *plus* overhead → slower than one thread alone (4.2 s vs ~0.4 s).

### 4.10 Cache lines and cache coherence (why v1 is slow although it never waits)

Memory moves between RAM and the CPU caches in blocks of **64 bytes** (a cache line). To write, a core must own the line
**exclusively** (the MESI cache-coherence protocol invalidates other cores' copies). The three shared counters sit in one
cache line, so with 32 threads writing, that line constantly bounces between cores. Every increment becomes a trip between cores
(~100 ns instead of ~1 ns). This is also called *cache-line ping-pong*. If different threads write different variables that
happen to share a line, it is called **false sharing**.

### 4.11 Per-thread accumulation (the right way)

Give every thread its **own** counters (local variables, kept in registers), and combine the results **once**, after all
threads have finished (`Join`). No sharing during the work → no race, no lock, no cache-line bouncing. This pattern
(split → work independently → combine) is called *map–reduce* or *fork–join*. Task 1b v3: **12.4 ms**, correct.

### 4.12 Producer–consumer, blocking queue, back-pressure, poison pill

- A **producer** creates work items, **consumers** process them, a **thread-safe queue** connects them.
- A **blocking** queue makes threads wait: `Add`/`put` waits while the queue is full, `Take`/`take` waits while it is empty.
- A **bounded** queue (max 100 batches) gives **back-pressure**: a fast producer is slowed to the consumers' speed, so memory
  stays limited. An **unbounded** queue can grow until memory runs out.
- **Batching** (10,000 lines per item) spreads the cost of one queue operation (lock + maybe waking a thread) over many lines.
- **Closing:** C# `BlockingCollection.CompleteAdding()`; Java has no close, so the producer sends a **poison pill** (a special
  END item, one per consumer).

### 4.13 The OS file cache, and why the lab demands re-plugging

Windows keeps recently read file data in RAM (the **file cache** / page cache). The second time you read a file, it comes from
RAM, not from the device. The USB files read again from the cache took **0.35 s** instead of **30.3 s**. Re-plugging (or ejecting)
**dismounts** the volume, so Windows throws away everything it had cached for it, and the next read must come from the flash.
`FILE_FLAG_NO_BUFFERING` (`--cache off`) is the software alternative: Windows reads straight from the device into your buffer.
The buffer address and read size must then be multiples of the sector size (we use 4096).

### 4.14 USB 2.0 and slow flash

USB 2.0 High-Speed is 480 Mbit/s on the wire; in practice ~35–40 MB/s at most, and cheap flash is slower: this stick read at
**~11.7 MB/s** and wrote at **4.5 MB/s**. A USB stick uses the *Bulk-Only Transport* protocol: it handles **one command at a
time**. Requests from several threads simply queue up behind each other. So more threads cannot read faster from one stick.

### 4.15 JIT compilation (why Java behaved differently)

Both C# and Java compile to an intermediate code (IL / bytecode). A **JIT** (just-in-time) compiler turns it into machine code
while the program runs.
- **C#:** this project sets `<TieredCompilation>false</TieredCompilation>`, so every method is compiled to **fully optimised**
  code before its first call. There is no slow phase inside the measurement.
- **Java (HotSpot):** a method starts in the **interpreter** (slow), then gets compiled by C1 (fast compile, with profiling),
  then by C2 (optimised, based on the profile). A long loop is replaced while it runs (**OSR**, on-stack replacement). If the
  optimised code meets a case it assumed would never happen, it is **deoptimised** ("made not entrant: uncommon trap") and
  rebuilt later. All of this happens on compiler threads that need CPU time.

### 4.16 Memory: peak working set, garbage collection

- **Working set** = the physical RAM pages a process uses now; **peak working set** = the maximum ever (what we report).
- .NET and Java free memory with a **garbage collector**. Objects ≥ 85,000 bytes go to .NET's *Large Object Heap*, which is
  collected rarely. That's why Task 2 reuses one buffer per thread instead of `File.ReadAllBytes`.
- The JVM sizes its heap generously (up to ¼ of RAM by default) and collects later, so Java's peaks are higher.

---

## 5. The data

`Lab1.exe gen` (`DataGenerator.cs`) writes:

| File | Content | Size |
|---|---|---|
| `data/numbers.txt` | 50,000,000 random integers in [−1,000,000; 1,000,000], one per line, `\n` endings, seed 12345 | 369,454,226 bytes (352 MB) |
| `data/parts/numbers_1..8.txt` | the same numbers in order, 6,250,000 per file | ~46.18 MB (44 MiB) each |
| `data/numbers.expected.txt` | the correct totals: `25007776 24992202 22` | 20 bytes |

**Correct totals:** negative **25,007,776**, positive **24,992,202**, zero **22**, total **50,000,000**.
(Zero is rare: 1 value out of 2,000,001 → expected 25 in 50 million; 22 is normal.)

`Random(12345)` gives the same "random" sequence every time, so the file is reproducible. Each number is written to
`numbers.txt` and to the current part file **at the same moment**, so the parts are exactly `numbers.txt` cut in 8 pieces.
`data/` is not in Git (`numbers.txt` is over GitHub's 100 MB file limit); recreate it with `gen`.

---

## 6. Map of the files

| File | What it does |
|---|---|
| `Program.cs` | entry point: reads the first word (`gen`, `task1a`...) and runs that task; help text |
| `CommandLine.cs` | tiny parser: `task1a --threads 8` → command + options dictionary |
| `Paths.cs` | where the data files are |
| `DataGenerator.cs` | creates the data and the expected totals |
| `SignCounts.cs` | the three counters (a `struct`) + `Add`, `+`, `SameAs` |
| `NumberParser.cs` | turns raw bytes (`-42\n7\n`) into numbers, one byte at a time, keeping state between buffers |
| `ThreadRunner.cs` | `RunAll(n, body)`: start n OS threads, wait for all; `Split`: divide a range into n parts |
| `Task1a.cs` | file split into n byte ranges, one thread per range, boundary rule |
| `Task1b.cs` | three counting versions on an `int[]` |
| `Task1c.cs` | reader + workers + bounded `BlockingCollection`; measures `queueMax` |
| `Task2.cs` | 8 files, one read per file into one buffer; I/O vs CPU timing; `--cache off` |
| `Report.cs` | prints time + peak memory + CORRECT/WRONG, appends CSV rows |
| `SystemInfo.cs` | `info` command |
| `Lab1.csproj` | project settings: .NET 10, Release optimisations, tiered compilation off, invariant culture |
| `java/src/lab1/*.java` | the Java version: same file names (`Task1a.java`...), plus `PeakMemory.java` |
| `common.ps1` | shared script helpers: build C#, find/build Java, run one configuration |
| `run_task1.ps1` | runs all Task 1 configurations (`-Lang`, `-Runs`, `-Extras`, `-Tasks`) |
| `run_task2.ps1` | Task 2 with automatic eject + wait for replug (`-Copy`, `-Lang`, `-Warm`, `-StartAt`, `-Manual`) |
| `results/*.csv` | every measurement (`lang,task,variant,threads,time_ms,peak_memory_mb,negative,positive,zero,total,check,io_ms,cpu_ms,queue_max`) |
| `results/summary.md` | median tables, generated by the report builder |
| `report/` | `build_report.mjs` (document), `charts.mjs` (SVG charts), `data.mjs` (CSV + medians), `docxkit.mjs` (Word helpers), `charts/*.png`, the `.docx` |

---

## 7. Code walkthrough

The code itself has only short comments, one sentence where something important happens. The full explanations are here:
the big picture of each file and the parts you must be able to explain.

### 7.1 `Program.cs` and `CommandLine.cs`

`Program.cs` uses *top-level statements* (no `class Program`/`Main` written by hand). It creates a `CommandLine` from `args`
and `switch`es on the command. Only *expected* errors (bad option, missing file) are caught and printed; real bugs crash with
a stack trace. `CommandLine` stores `--name value` pairs in a `Dictionary<string,string>`; `GetInt` uses `int.TryParse`.

### 7.2 `SignCounts.cs` and `NumberParser.cs`

`SignCounts` is a **struct** (value type): `var local = new SignCounts();` inside a thread is that thread's private copy;
assigning it copies the three `long`s. `operator +` lets us write `total += part`.

`NumberParser` reads bytes, not strings:

```text
bytes:  '-'  '4'  '2'  '\n'  '7'  '\n'
state:  neg  4    42   → count -42, reset   7   → count 7, reset
```

- digit: `value = value * 10 + (b - '0')` (characters are numbers: `'0'` = 48, so `'4' - '0'` = 4);
- `-`: remember negative; `\n`: the number is complete → `counts.Add(...)`, reset; anything else (`\r`) ignored.
- The state survives between calls, so a number split across two 1 MB buffers ("-4" | "2\n") is still parsed correctly.
- `[MethodImpl(AggressiveInlining)]`: `Feed` is called ~370 million times; inlining removes the call overhead.

### 7.3 `ThreadRunner.cs`

```csharp
for (int t = 0; t < count; t++)
{
    int index = t;                                   // a NEW variable per iteration
    threads[t] = new Thread(() => body(index));      // create an OS thread (not started yet)
    threads[t].Start();                              // now the OS schedules it
}
foreach (Thread thread in threads) thread.Join();    // wait until every thread has finished
```

- **Why `int index = t`?** A lambda *captures variables, not values*. All iterations of a `for` loop share **one** `t`, so a
  thread that starts late could see `t` already changed (two threads with the same part, one part never counted). Copying it
  into a variable declared *inside* the loop gives each lambda its own copy. Java refuses to compile the wrong version.
- **`Join`** blocks until the thread ends. It also guarantees that everything the thread wrote is visible afterwards. That's
  why reading `perThread[t]` after `RunAll` is safe without a lock.
- **`Split(length, i, n)`** returns `[length*i/n, length*(i+1)/n)`: pieces that never overlap and together cover everything
  (integer division puts the remainder into later pieces).

### 7.4 `Task1a.cs`: the boundary rule (control question 2)

The file is split into N equal **byte** ranges `[start, end)`. A cut usually falls inside a line. **Rule: a line belongs to the
thread whose range contains the line's first byte.**

1. A thread with `start > 0` begins reading at `start − 1` to see the byte **before** its range.
   - If that byte is `\n`, a line starts exactly at `start`: it's ours.
   - Otherwise the line started earlier, belongs to the previous thread → **skip** bytes until the next `\n`.
2. It parses its range. When a `\n` is at position `p` and `p + 1 >= end`, the next line would start at or after `end`, so it
   belongs to the next thread → **stop**. A thread therefore reads a few bytes past `end` to finish the line it started.

Example, `...\n-12|3456\n...` with the cut `|` between thread A and B: the line `-123456` starts before the cut → A finishes it
(reads past `end`), B sees that the byte before its start is `2` (not `\n`) and skips to the `\n`. Every line has exactly one
first byte, ranges don't overlap and cover the file → **each line counted exactly once**. The proof in practice: CORRECT in all
runs with 1–64 threads.

Each thread opens **its own handle** and uses `RandomAccess.Read(handle, buffer, offset)`: a read at an *explicit* offset, no
shared "current position" that threads could disturb. 1 MB chunks; after `end` only 64 bytes at a time (a line is ≤ 9 bytes).

### 7.5 `Task1b.cs`: three ways to count

- `LoadNumbers` (not timed): `File.ReadLines` + `int.Parse` into a `List<int>(50_000_000)` → `ToArray()`; then `GC.Collect()`
  so garbage collection doesn't run during a measurement.
- `SharedCounters` is a **class**: all threads get a reference to **the same object**.
- **v1** `shared.Negative++` without protection → race (section 4.8) + cache-line bouncing (4.10).
- **v2** `lock (Gate) { ... }` around every increment. `Gate` is a `System.Threading.Lock` (.NET 9+); `lock` = enter/exit.
- **v3** `var local = new SignCounts();` per thread, `perThread[t] = local` once at the end, summed after `RunAll`.
- `Measure` takes a `Func<SignCounts>` (a function value) and times it with `Stopwatch`.

### 7.6 `Task1c.cs`: producer and consumers

```csharp
using var queue = new BlockingCollection<List<string>>(boundedCapacity: 100);
var reader = new Thread(() => queueMax = ReadBatches(path, batchSize, queue));   // the producer
reader.Start();
ThreadRunner.RunAll(workerCount, w => perWorker[w] = CountBatches(queue));         // the consumers
reader.Join();
```

- **Reader:** `StreamReader.ReadLine()` → `List<string>` of 10,000 lines → `queue.Add(batch)` (**blocks** while 100 batches are
  waiting) → a **new** list for the next batch (the old one now belongs to a worker). After each `Add` it records
  `queue.Count` → `queueMax`. In `finally`: `queue.CompleteAdding()`, so workers never wait forever, even after an error.
- **Workers:** `foreach (var batch in queue.GetConsumingEnumerable())`: each batch goes to **exactly one** worker, the loop
  **waits** while the queue is empty, and **ends** once `CompleteAdding` was called and the queue is empty. Each worker counts
  into its own `local` (like v3).
- `--batch 1` and `--capacity 0` (unbounded) exist for question 6.

### 7.7 `Task2.cs`: files on slow storage

- The 8 files are split among threads with `Split` (1 thread: 8 files; 2: 4+4; 4: 2+2+2+2).
- **One buffer per thread, reused**, sized for its largest file. Why not `File.ReadAllBytes`? It creates a new 44 MB array per
  file; such arrays live on the Large Object Heap, which is collected rarely, so the peak memory would be ~380 MB with any
  thread count, hiding the real "threads × buffer" effect.
- **One read per file:** `RandomAccess.Read(file, buffer.AsSpan(...), 0)` asks for the whole file. The `while` loop is only a
  safety net, because the OS may return fewer bytes than asked.
- **Timing:** `Stopwatch.GetTimestamp()` before opening (t0), after reading (t1), after parsing (t2). `t1 − t0` = I/O,
  `t2 − t1` = CPU. Each thread adds to **its own** array slot (`ioTicks[t] += ...`), so there is no race. Printed as sums over
  threads + the I/O share in %.
- **`--cache off`:** `FileOptions` value `0x20000000` = `FILE_FLAG_NO_BUFFERING`. Rules: buffer address, file offset and
  length must be multiples of the sector size. So the buffer is a **pinned** array
  (`GC.AllocateUninitializedArray(..., pinned: true)`; the GC will never move it), we find the first 4096-aligned byte in it
  (`Marshal.UnsafeAddrOfPinnedArrayElement` + `%`), and we ask for the length rounded **up** to 4096. Windows still returns
  only the real bytes.

### 7.8 `Report.cs`: what is measured

- **Time:** `Stopwatch` (high-resolution timer) from before the threads are created to after the last `Join`.
- **Peak memory:** `Process.GetCurrentProcess().PeakWorkingSet64`, the Windows `PeakWorkingSetSize` counter. Each
  configuration runs in a **new process**, so the peak belongs to that configuration only (Task 1b's 5 measurements share one
  process; its memory is dominated by the array anyway).
- **Check:** compares with `data/numbers.expected.txt` → CORRECT / WRONG / UNCHECKED.
- **CSV:** appends one row; empty cells = not measured.

### 7.9 The Java version (`java/src/lab1/`)

The same files and logic. The differences you should know are in [section 12](#12-c-vs-java-the-bonus). In short:
`Thread`/`start()`/`join()`, `synchronized`, `ArrayBlockingQueue` + poison pill, `FileChannel.read(buffer, position)`,
`ByteBuffer.allocateDirect` for Task 2, and `PeakMemory.java`, which calls Windows' `K32GetProcessMemoryInfo` through the
Foreign Function API (Java has no peak-memory method). That's why it needs `--enable-native-access=ALL-UNNAMED`.

### 7.10 The scripts

- `common.ps1`: `Build-CSharp` (returns the exe path), `Find-JavaBin` (PATH → JAVA_HOME → Program Files), `Build-Java`
  (`javac`), `Invoke-Lab1` (runs C# or Java, throws on a non-zero exit code).
- `run_task1.ps1`: warm-up run (puts `numbers.txt` in the file cache), then 1a / 1b / 1c × `-Runs`, then the extras.
- `run_task2.ps1`:
  - `Find-LabDrive`: the removable volume that contains `lab1_task2\numbers_1.txt` (so a changed drive letter doesn't matter);
  - `Get-UsbDeviceId`: drive letter → partition → disk (`USBSTOR\...`) → its parent USB device (`USB\VID_048D&PID_1234\...`);
  - `[UsbEject]::Eject(id)`: C# code compiled inside PowerShell (`Add-Type`) that calls `CM_Request_Device_Eject` from
    `cfgmgr32.dll`, the "Safely Remove Hardware" function, no admin rights needed;
  - `Wait-ForReplug`: eject → notice (beep + pop-up) → wait until gone → wait until back **and stable for 8 s**;
  - retry up to 5 times if the run fails (e.g., the stick disconnected); SHA-256 check of all 8 files at the end.

### 7.11 The report generator (`report/`)

`data.mjs` reads every `results/*.csv` (skipping the old format) and computes medians per configuration. `charts.mjs` writes
each chart as SVG text (lines, dots, bars) and converts it to PNG with `resvg` at 3× resolution. Colours: C# blue / Java orange,
checked for colour-blind safety. `build_report.mjs` writes the Word document with the `docx` library: title page, tables,
figures, answers with the numbers inserted from the data, and `results/summary.md`. If you measure again, run
`node build_report.mjs` and the report updates itself.

---

## 8. Results and what they mean

Medians of 5 runs (Task 1) or single runs after a replug (Task 2). Every run gave the correct totals except Task 1b v1.
Full tables: [`results/summary.md`](results/summary.md); charts: `report/charts/`.

### 8.1 Task 1a

| Threads | C# time | C# speed-up | C# memory | Java time | Java speed-up | Java memory |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 398 ms | 1.00× | 26.2 MB | 583 ms | 1.00× | 58.2 MB |
| 2 | 203 ms | 1.96× | 27.2 MB | 313 ms | 1.86× | 59.4 MB |
| 4 | 110 ms | 3.63× | 29.8 MB | 181 ms | 3.23× | 61.4 MB |
| 8 | 63.1 ms | 6.31× | 33.7 MB | **131 ms** | **4.46×** | 65.8 MB |
| 16 | 39.0 ms | 10.2× | 42.1 MB | 159 ms | 3.67× | 77.3 MB |
| 32 | **35.1 ms** | **11.3×** | 58.3 MB | 213 ms | 2.74× | 135.4 MB |
| 64 | 38.8 ms | 10.3× | 90.4 MB | 224 ms | 2.61× | 187.4 MB |

- C#: almost ideal to 8, strong to **16 (physical cores)**, a little more to **32 (logical)**, worse at 64.
- Memory grows ~1 MB per thread in C# = each thread's 1 MB read buffer. Java grows ~2 MB per thread (plus a temporary native buffer).
- Java stops at 8: JIT warm-up (section 12).

### 8.2 Task 1b (32 threads)

| Version | C# time | C# total | Java time | Java total |
|---|---:|---:|---:|---:|
| v1 no sync, run 1 | 469 ms | 3,437,374 WRONG | 316 ms | 3,923,290 WRONG |
| v1 no sync, run 2 | 235 ms | 5,373,705 WRONG | 210 ms | 2,784,793 WRONG |
| v1 no sync, run 3 | 247 ms | 5,233,149 WRONG | 164 ms | 2,795,938 WRONG |
| v2 lock (median) | 4,212 ms | 50,000,000 | 2,055 ms | 50,000,000 |
| v3 per-thread (median) | **12.4 ms** | 50,000,000 | 107 ms | 50,000,000 |

Ranking (fast → slow): **v3 < v1 < v2**. Correct: v2 and v3. v1 lost 88–95 % of the increments, and gives a different
wrong total every run. Note that zero is always right (22): zeros are so rare that two threads almost never increment
`Zero` at the same moment.

### 8.3 Task 1c (x = workers, plus 1 reader)

| Workers | C# time | C# memory | max batches in queue (C#) | Java time | Java memory | max batches (Java) |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1,568 ms | 78.2 MB | 46 | 1,027 ms | 369.1 MB | 5 |
| 2 | **1,021 ms** | 44.5 MB | 4 | 1,023 ms | 369.7 MB | 16 |
| 4 | 1,186 ms | 45.1 MB | 2 | 1,023 ms | 365.6 MB | 35 |
| 8 | 1,265 ms | 54.9 MB | 2 | 1,035 ms | 377.9 MB | 100 |
| 16 | 1,645 ms | 64.5 MB | 1 | 1,051 ms | 375.9 MB | 100 |
| 32 | 2,127 ms | 82.1 MB | 2 | 1,050 ms | 367.6 MB | 46 |
| 64 | 2,192 ms | 134.7 MB | 2 | 1,076 ms | 462.5 MB | 47 |

**How to read "max batches in queue":** near 0 → the workers are *waiting for the reader* (the reader is the bottleneck);
near 100 → the reader is *waiting for the workers*. C# with 1 worker: up to 46 waiting → one C# worker is a bit slower than the
reader. From 2 workers: ≤ 4 → the reader is the bottleneck, so more workers can't help, and they only add contention (slower).
Java: one worker is already faster than the reader (≤ 5). The occasional 100 with 8+ Java workers are short stalls (e.g., GC
pauses) that don't change the time much.

**Question 6 experiments:** 1 line per batch → C# **11.0 s** (1 worker) / **31.2 s** (4 workers), Java 7.3 s / 16.1 s,
instead of ~1–1.6 s. Unbounded queue: no change here (C# 1 worker held at most 43 batches, never reaching 100), because the
workers are about as fast as the reader.

### 8.4 Task 2 (USB 2.0 stick, replugged before every run)

| Threads | C# time | C# memory | C# I/O (sum of threads) | C# parse (sum) | same, from cache |
|---:|---:|---:|---:|---:|---:|
| 1 | **30.3 s** | 71.0 MB | 29.9 s (98.9 %) | 323 ms | 348 ms |
| 2 | 30.0 s | 115.5 MB | 55.8 s | 310 ms | 186 ms |
| 4 | 29.9 s | 203.3 MB | 96.8 s | 317 ms | 116 ms |

Java 1 thread: **30.5 s** (I/O 30.0 s, parse 477 ms), 96 MB. Java 2/4 threads: not completed (the stick kept disconnecting).

Same code on the internal NVMe SSD with `--cache off`: 1 thread 417 ms (I/O only 120 ms), 2 → 244 ms, 4 → 170 ms, 8 → 139 ms.
**On the SSD the program is CPU-bound and more threads help; on the USB stick it is I/O-bound and they don't.**

---

## 9. Answers to the report questions (short)

The full answers with all numbers are in the Word report (sections 4.4 and 5.1). Here they are in short form, to remember.

**Task 1**

1. **Where does speed-up stop?** C#: grows to 16 threads (= 16 physical cores, 10.2×), a little more to 32 (= logical
   processors, 11.3×; SMT adds ~11 %), stops there; 64 is slower. Java: best at 8 (JIT warm-up limits it).
2. **Why don't 32/64 help?** Only 32 logical processors: 64 threads must take turns (context switches, cache pollution),
   and each thread costs creation, stack, a 1 MB buffer, a file handle. 32 helps only a bit because SMT threads share a
   core. 64 is slower than 32.
3. **Memory vs threads?** Yes: ~1 MB per thread in 1a (each thread's buffer). In 1c, memory follows the number of batches
   alive (queue + one per worker). 1b: constant (the 200 MB array dominates).
4. **1b ranking:** v3 (12 ms) < v1 (~240 ms, WRONG) < v2 (~4.2 s). v1: lost updates + cache-line bouncing. v2: correct but
   50 M lock operations with 32 threads fighting = serial + overhead. v3: private counters, combined once → correct and fastest.
5. **1c vs 1a:** 1a is ~29× faster. In 1c one reader does all the reading, line splitting and 50 M string creations
   (serial, ~1 s; Amdahl). 1c is fastest at 2 workers (C#), already at 1 worker (Java); after that only slower.
6. **1-line batches:** 50 M queue operations instead of 5,000 → 11–31 s. **Unbounded queue:** no back-pressure; if consumers
   are slower, memory grows without limit (here they kept up, so no difference).

**Task 2**

1. **USB 1 thread vs 1a 1 thread:** 30.3 s vs 0.4 s (76×). 98.9 % of the time waiting for the drive; parsing 323 ms
   (the same work as 1a). From the cache: 0.35 s. C# and Java take the same time. → **I/O-bound.**
2. **Do more threads help?** No (30.3 / 30.0 / 29.9 s), memory grows. All threads wait for **one** device with a fixed
   bandwidth that serves one request at a time. Concurrency helps I/O-bound work only when the waits are **independent**
   (many different servers), not when everybody waits for the same slow device.

**Conclusion:** CPU-bound = Task 1 (data in RAM) and Task 2 on the SSD (more threads → faster; parsing dominates). I/O-bound =
Task 2 on the USB stick (99 % waiting; threads don't help; the cache makes it 87× faster; the language doesn't matter).

---

## 10. Control questions: answers for the defense

These are answered **orally** (grades 9 and 10). Learn the idea and one number from your own measurements for each.

**1. What is the difference between a physical core and a logical processor? Where did it show in your charts?**
A physical core is a complete execution unit (ALUs, registers, L1/L2 cache). A logical processor is what the OS schedules on.
With SMT each core runs two hardware threads, so 16 cores = 32 logical processors. The two share the core's execution units,
so the second one only uses the gaps of the first (lecture: 15–30 %). In Task 1a (C#) the speed-up rose steeply up to 16 threads
(10.2×), then only to 11.3× at 32 (+11 %), and dropped at 64. The bend at 16 is the physical cores; the end at 32 is the logical
processors.

**2. How did you make sure that no line was counted twice or skipped at the boundary between two parts of the file?**
Rule: a line belongs to the thread whose byte range contains the line's *first byte*. A thread (except the first) looks at
the byte just before its range: if it isn't `\n`, the current line belongs to the previous thread, so it skips to the next `\n`.
At the end of its range a thread keeps reading until it finishes the line it started, and it stops at the `\n` after which the
next line would start at or past `end`. The ranges don't overlap and cover the whole file, and every line has exactly one first
byte, so every line is counted by exactly one thread. Checked: 50,000,000 and the same totals for 1–64 threads.

**3. Explain step by step how unsynchronised shared counters lose updates.**
`counter++` = read into a register, add 1, write back. Thread A reads 100; thread B reads 100; A writes 101; B writes 101.
Two increments, but the counter only grew by 1. With 32 threads this happens constantly: 88–95 % of the increments were
lost, and every run gave a different total (e.g., 3.4 M and 5.4 M instead of 50 M). (See the table in section 4.8.)

**4. Why are per-thread counters faster than shared counters protected by a lock?**
With a lock, every one of the 50 M increments must take and release the same lock. Those are atomic operations on one shared
cache line, and with 32 threads fighting for it most threads wait (spin or sleep and get woken up), so the counting is effectively
done by one thread at a time, plus the overhead. Per-thread counters are local variables (registers): nothing is shared during
the loop, so there is no waiting, no lock and no cache-line traffic; the results are combined once after `Join`. Measured:
12.4 ms vs 4.2 s (≈ 340×).

**5. What does "I/O-bound" mean for a USB drive, and for a server waiting on the network? Why does concurrency help in only one of the two cases?**
I/O-bound = the time is spent waiting for an input/output device, not computing. For the USB stick the waiting is for **one
device with a fixed bandwidth** (~11.7 MB/s) that handles one request at a time. More threads only queue more requests in front
of the same device, so the total stays ~30 s. For a server waiting on the network, each request waits for a **different remote
machine** (or for latency that is independent per request). The waits can overlap: while one request waits, another one can be
sent and processed, so 100 concurrent requests take about as long as one. Concurrency helps when the waits are independent and
the resource isn't saturated, and it doesn't help when everybody waits for the same saturated resource.

**6. What is the difference between dividing the work into equal parts in advance and taking it from a queue? When is each one better?**
- *Static division* (Task 1a, Task 2): each thread gets a fixed part up front. There's no coordination during the work, so the
  overhead is minimal. That's best when the work is **uniform and known in advance** (same-size pieces, same speed), like our
  file. Its weakness: if one part is slower (uneven data, a slow core), everyone waits for the slowest (load imbalance).
- *Queue / dynamic division* (Task 1c): threads take the next piece when they are free. It **balances the load automatically**
  and works when the amount of work is **not known in advance** or arrives over time (a stream, network requests, a producer).
  Its costs: synchronisation on every take (so batches must be big enough), and a producer that can become the bottleneck.
  It's better for uneven or streaming work.

---

## 11. Defense cheat-sheet: questions about the code

- **Where are the threads created?** `ThreadRunner.RunAll` (`new Thread(...)`, `Start`, `Join`); in Task 1c also the reader
  `new Thread(...)` in `Task1c.Run`.
- **Are these real OS threads?** Yes: `System.Threading.Thread` = one OS thread each (not the thread pool, not `Task`).
- **Why `int index = t` before the lambda?** The lambda captures the variable `t`, which all iterations share. Without the copy
  two threads could get the same index.
- **How do threads return their results?** Each writes its own slot `perThread[t]`; the main thread reads them after `Join`.
  Different array elements → no race; `Join` makes the writes visible.
- **What does `Join` do?** It blocks the caller until that thread has finished.
- **Why `long` counters?** No overflow risk, and they cost nothing here.
- **Why parse bytes instead of `ReadLine` + `int.Parse` in 1a/2?** It avoids creating 50 million strings (allocation + GC);
  it is several times faster, and it makes the CPU part small and fair to compare.
- **Why `RandomAccess.Read` with an offset?** Each thread reads at an explicit position, with no shared file pointer.
- **What is `BlockingCollection`?** .NET's thread-safe blocking queue: `Add` waits when full (bounded), `Take`/
  `GetConsumingEnumerable` waits when empty, `CompleteAdding` closes it.
- **Why is `CompleteAdding` in `finally`?** So workers are released even if the reader crashes; otherwise they would wait forever.
- **Why a new `List` after each `Add`?** The added list now belongs to a worker; reusing it would be a race.
- **Why does Task 2 reuse one buffer per thread?** Peak memory then shows "threads × file size", and big arrays aren't
  re-allocated on the Large Object Heap.
- **What does "one read operation" mean?** One `RandomAccess.Read` call asking for the whole file into one `byte[]`.
- **How is the cache avoided in Task 2?** By the replug (dismount clears the cache). Extra: `--cache off` =
  `FILE_FLAG_NO_BUFFERING` with a sector-aligned, pinned buffer.
- **How is peak memory measured?** `Process.PeakWorkingSet64`, the max physical RAM of the process; a new process per run.
- **How is time measured?** `Stopwatch` around thread creation → last `Join`; data loading (1b) excluded.
- **Why `TieredCompilation=false`?** Methods are fully optimised from the first call, so the JIT warming up doesn't distort
  short measurements.
- **Why Release?** Debug builds are not optimised; timings would be meaningless.
- **What is `lock`?** Mutual exclusion: `Monitor.Enter`/`Exit` (or `Lock.EnterScope`) around the block. Only one thread inside.
- **Could v1 be fixed without a lock?** Yes: `Interlocked.Increment(ref shared.Negative)` (an atomic add). It's correct, but
  still slow because of the cache-line contention. v3 is the real fix.

---

## 12. C# vs Java (the bonus)

| | C# (.NET 10) | Java 25 |
|---|---|---|
| OS thread | `new Thread(() => ...)`, `Start()`, `Join()` | `new Thread(() -> ...)`, `start()`, `join()` (platform thread) |
| Loop variable in lambda | compiles; `for` variable is shared → copy needed | compile error unless effectively final → copy forced |
| Lock | `lock (obj) { }` (`System.Threading.Lock`) | `synchronized (obj) { }` (any object), `ReentrantLock` |
| Bounded blocking queue | `BlockingCollection<T>(boundedCapacity)` | `ArrayBlockingQueue<T>(capacity)` |
| Unbounded | `new BlockingCollection<T>()` | `new LinkedBlockingQueue<>()` |
| Close the queue | `CompleteAdding()` + `GetConsumingEnumerable()` | no close → **poison pill** (one END per worker) |
| Per-thread result | `struct` (value type, copied) | `class` (reference; one `new` per thread) |
| Read at offset | `RandomAccess.Read(handle, span, offset)` | `FileChannel.read(ByteBuffer, position)` |
| Whole file → buffer | `byte[]` | direct `ByteBuffer` (OS reads straight into it) |
| Peak memory | `Process.PeakWorkingSet64` | no API → `K32GetProcessMemoryInfo` via Foreign Function API |
| Timer | `Stopwatch` | `System.nanoTime()` |
| Optional parameters | `double? x = null` | overloads + `Double` (boxed, nullable) |
| Exception in a thread | crashes the process | thread dies; `ThreadRunner` collects and re-throws |
| Truly parallel? | yes, OS threads, no global lock | yes, OS threads, no GIL (unlike CPython) |

**Measured differences:**

- **Warm-up:** Java starts in the interpreter, compiles while running, and sometimes throws optimised code away and redoes it.
  With 8 threads the optimised loop was ready 54 ms after start; with 32 threads only after 103 ms (the compiler threads
  compete with 32 workers), plus several deoptimisations ("made not entrant: uncommon trap"). Hence Java 1a is best at 8 threads
  and 1b v3 takes 107 ms vs 12 ms in C# (which compiles everything optimised before the first call).
  You can see it yourself: `java -XX:+PrintCompilation --enable-native-access=ALL-UNNAMED -cp java/out lab1.Main task1a --threads 32`.
- **Memory:** the JVM starts bigger (58 vs 26 MB) and lets its heap grow (1c: ~365 MB vs ~45 MB).
- **Strings:** Java stores Latin-1 text with 1 byte per character, .NET with 2 (UTF-16), so Java's 1c reader is faster
  (~1.0 s flat).
- **Heavy lock contention:** Java's `synchronized` 2.1 s vs C#'s `lock` 4.2 s. Both are the wrong tool here.
- **I/O-bound:** identical (30.3 vs 30.5 s on the USB).

---

## 13. Glossary

| Term | Meaning |
|---|---|
| Thread | one flow of execution: program counter, registers, stack; shares the process's memory |
| OS / platform thread | a thread scheduled by the operating system (what `new Thread` creates) |
| Logical processor | a hardware thread the OS can schedule on (32 here) |
| SMT / Hyper-Threading | 2 hardware threads per physical core, sharing its execution units |
| Speed-up | T(1) / T(n) |
| Amdahl's law | the serial fraction s limits the speed-up to 1/s |
| Race condition | the result depends on the timing between threads |
| Lost update | an increment overwritten by another thread's increment |
| Mutex / lock | lets only one thread at a time into a critical section |
| Contention | many threads wanting the same lock / cache line at once |
| Cache line | the 64-byte block in which memory moves between RAM and caches |
| False sharing | different variables in the same cache line, written by different threads |
| Producer–consumer | one side creates work, the other consumes it, through a queue |
| Back-pressure | a full queue slows down the producer |
| Poison pill | a special item that tells a consumer to stop |
| File / page cache | file data the OS keeps in RAM after reading |
| Working set | physical RAM a process uses; *peak* = the maximum |
| JIT | compiles intermediate code to machine code while the program runs |
| OSR | Java replacing a running loop with compiled code |
| Deoptimisation | throwing away optimised code whose assumptions turned out false |
| CPU-bound / I/O-bound | limited by computing / by waiting for devices |
| Median | the middle value of sorted measurements (robust to one slow run) |

---

## 14. Troubleshooting

- **"... does not exist. Run 'gen' first"**: create the data: `Lab1.exe gen`.
- **`run_task2.ps1` says no drive has `lab1_task2`**: run it once with `-Copy` (copies `data\parts` to the USB drive).
- **The eject is refused**: close Explorer windows/programs using the drive, or use `-Manual` and eject from the tray icon.
- **"A device which does not exist was specified" during Task 2**: the USB stick disconnected (see section 3). The script repeats
  the run after a replug; a different USB port or a better stick helps. Check the System log for "disk" event 51 errors.
- **Java: "No JDK found"**: install one (`winget install EclipseAdoptium.Temurin.25.JDK`) or set `JAVA_HOME`.
- **Java warning about native access**: run with `--enable-native-access=ALL-UNNAMED` (needed for `PeakMemory`).
- **Slow or noisy timings**: plug in the laptop, close other programs, use the "Performance" power plan, and use the median of
  several runs (`-Runs 5`).
- **After the lab:** the folder `D:\lab1_task2` can be deleted; nothing else on the stick was changed. If you want to use the
  other ~92 GB of this 124 GB stick, test it first with a capacity checker (e.g. H2testw). Its ID and the many read errors
  suggest it may not really hold 124 GB.

### Hardware used

| | |
|---|---|
| CPU | AMD Ryzen 9 9955HX, 16 cores / 32 logical processors (SMT), base 2.5 GHz |
| RAM | 32 GB DDR5-5600 (2 × 16 GB Micron) |
| Disk | Micron MTFDKBA1T0QGN, 1 TB NVMe SSD |
| USB drive | "VendorCo ProductCode" (VID 048D / PID 1234), 124 GB reported, FAT32 32 GB partition, **USB 2.0 High-Speed**; read ~11.7 MB/s, write 4.5 MB/s |
| OS | Windows 11 Pro 64-bit (10.0.26200), power plan "Performance", on AC power |
| C# | C# 14, .NET SDK 10.0.401, runtime 10.0.12 (x64) |
| Java | Eclipse Temurin 25.0.5+8 LTS (HotSpot, G1 GC) |
