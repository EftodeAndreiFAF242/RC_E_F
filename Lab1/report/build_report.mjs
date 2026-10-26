// Builds the Word report, its charts and results/summary.md from every CSV in ../results (npm install, then node build_report.mjs).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
    AlignmentType, Document, Footer, HeadingLevel, Packer, PageBreak, PageNumber, Paragraph, TextRun,
} from 'docx';
import { lineChart, barChart, stackedBarChart, toPng, COLORS } from './charts.mjs';
import { loadRows, median, summarize, speedups, pick } from './data.mjs';
import {
    P, H1, H2, H3, bullet, numbered, qa, code, table, figure, link, spacer, numberingConfig, runs, FONT,
} from './docxkit.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const resultsDir = path.join(here, '..', 'results');
const chartsDir = path.join(here, 'charts');
fs.mkdirSync(chartsDir, { recursive: true });

const REPO = 'https://github.com/EftodeAndreiFAF242/RC_E_F';
const STUDENT = 'Eftode Andrei';
const GROUP = 'FAF-242';
const TEACHER = 'Cristofor Fistic';

// ------------------------------------------------------------------ data
const rows = loadRows(resultsDir);
const T = [1, 2, 4, 8, 16, 32, 64];
const T2 = [1, 2, 4];
const LANGS = ['csharp', 'java'];
const NAME = { csharp: 'C#', java: 'Java' };
const COLOR = { csharp: COLORS.csharp, java: COLORS.java };

const per = (fn) => Object.fromEntries(LANGS.map((l) => [l, fn(l)]));
const t1a = per((l) => summarize(pick(rows, { lang: l, task: 'task1a' }), T));
// Task 1c was measured again after adding the queue-length measurement; use those runs when present.
const newest1c = (list) => (list.some((r) => r.queue_max != null) ? list.filter((r) => r.queue_max != null) : list);
const t1c = per((l) => summarize(newest1c(pick(rows, { lang: l, task: 'task1c', variant: 'b10000-q100' })), T));
const t2cold = per((l) => summarize(pick(rows, { lang: l, task: 'task2', variant: 'cold' }), T2));
const t2warm = per((l) => summarize(pick(rows, { lang: l, task: 'task2', variant: 'warm' }), T2));
const T2SSD = [1, 2, 4, 8];
const t2ssdCold = summarize(pick(rows, { lang: 'csharp', task: 'task2', variant: 'ssd-nocache' }), T2SSD);
const t2ssdWarm = summarize(pick(rows, { lang: 'csharp', task: 'task2', variant: 'ssd-warm' }), T2SSD);

// Task 1b: the first complete "all" run gives the three version-1 results the lab asks for;
// the medians over all runs give the typical time of each version.
const t1b = per((l) => {
    const r = pick(rows, { lang: l, task: 'task1b' }).sort((a, b) => (a.file + String(a.order).padStart(5, '0')).localeCompare(b.file + String(b.order).padStart(5, '0')));
    const v = (name) => r.filter((x) => x.variant === name);
    return {
        firstV1: v('v1-no-sync').slice(0, 3),
        allV1: v('v1-no-sync'),
        v2: v('v2-lock'),
        v3: v('v3-local'),
        threads: r[0]?.threads,
        runs: v('v2-lock').length,
    };
});
const q6 = per((l) => ({
    single: newest1c(pick(rows, { lang: l, task: 'task1c', variant: 'b1-q100' })),
    unbounded: newest1c(pick(rows, { lang: l, task: 'task1c', variant: 'b10000-qinf' })),
}));

// ------------------------------------------------------------------ formatting
const n0 = (v) => (v == null ? '–' : Math.round(v).toLocaleString('en-US'));
const n1 = (v) => (v == null ? '–' : v.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 }));
const ms = (v) => (v == null ? '–' : v >= 100 ? `${n0(v)} ms` : `${n1(v)} ms`);
const sec = (v) => (v == null ? '–' : `${(v / 1000).toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} s`);
const mb = (v) => (v == null ? '–' : `${n1(v)} MB`);
const x = (v) => (v == null ? '–' : `${v.toFixed(v >= 10 ? 1 : 2)}×`);
const pct = (a, b) => `${((100 * a) / b).toFixed(1)} %`;
const best = (summary) => summary.filter((s) => s.time != null).reduce((a, b) => (b.time < a.time ? b : a));
const at = (summary, t) => summary.find((s) => s.threads === t);

// ------------------------------------------------------------------ charts
const figures = {};
function chart(name, svg) {
    const png = toPng(svg);
    fs.writeFileSync(path.join(chartsDir, `${name}.png`), png);
    const [, w, h] = svg.match(/width="(\d+)" height="(\d+)"/);
    figures[name] = { png, w: Number(w), h: Number(h) };
}
const coreLines = [{ x: 16, label: '16 physical cores', anchor: 'end' }, { x: 32, label: '32 logical processors' }];
const series = (data, field, scale = 1) => LANGS.map((l) => ({
    name: NAME[l], color: COLOR[l], values: data[l].map((s) => (s[field] == null ? null : s[field] / scale)),
}));
const speedSeries = (data) => LANGS.map((l) => ({ name: NAME[l], color: COLOR[l], values: speedups(data[l]) }));
const fmtMs = (v) => (v >= 100 ? `${Math.round(v).toLocaleString('en-US')} ms` : `${v.toFixed(1)} ms`);
const fmtX = (v) => `${v.toFixed(1)}×`;
const fmtMb = (v) => `${Math.round(v)} MB`;

/** The three charts the lab asks for: time, speed-up and peak memory against the number of threads. */
function threeCharts(prefix, data, xs, what, { refLines = [], xLabel, seconds = false, labelEnds = true, unitName = 'thread' } = {}) {
    chart(`${prefix}_time`, lineChart({
        title: `${what}: execution time`,
        subtitle: `${seconds ? 'seconds' : 'median of the runs, milliseconds'} (lower is better)`,
        xs, xLabel, series: series(data, 'time', seconds ? 1000 : 1), yLabel: seconds ? 'time, s' : 'time, ms', refLines,
        labelEnds, fmt: seconds ? (v) => `${v.toFixed(1)} s` : fmtMs,
    }));
    const sp = speedSeries(data);
    chart(`${prefix}_speedup`, lineChart({
        title: `${what}: speed-up relative to one ${unitName}`,
        subtitle: `time with 1 ${unitName} ÷ time with n ${unitName}s (higher is better)`,
        xs, xLabel, series: sp, yLabel: 'speed-up, ×', ideal: true, refLines, labelEnds, fmt: fmtX,
        yMax: Math.max(...sp.flatMap((s) => s.values.filter((v) => v != null))) * 1.25,
    }));
    chart(`${prefix}_memory`, lineChart({
        title: `${what}: peak memory`, subtitle: 'peak working set of the process, MB',
        xs, xLabel, series: series(data, 'memory'), yLabel: 'peak memory, MB', refLines, labelEnds, fmt: fmtMb,
    }));
}

threeCharts('task1a', t1a, T, 'Task 1a', { refLines: coreLines });
threeCharts('task1c', t1c, T, 'Task 1c', { refLines: coreLines, xLabel: 'number of worker threads (plus 1 reader thread)', unitName: 'worker' });
threeCharts('task2', t2cold, T2, 'Task 2 (USB drive, re-plugged before every run)', { seconds: true, labelEnds: false });

chart('task1b_time', barChart({
    title: `Task 1b: three ways to count (${t1b.csharp.threads} threads)`,
    subtitle: 'version 1: the three runs of the first measurement; versions 2 and 3: median, ms',
    unit: 'time, ms',
    legendItems: LANGS.map((l) => ({ name: NAME[l], color: COLOR[l] })),
    rows: [
        ...[0, 1, 2].map((i) => ({
            label: `v1 no sync, run ${i + 1}`,
            bars: LANGS.map((l) => ({ value: t1b[l].firstV1[i]?.time_ms ?? 0, color: COLOR[l], text: `${ms(t1b[l].firstV1[i]?.time_ms)}  WRONG` })),
        })),
        { label: 'v2 lock', bars: LANGS.map((l) => ({ value: median(t1b[l].v2.map((r) => r.time_ms)), color: COLOR[l], text: `${ms(median(t1b[l].v2.map((r) => r.time_ms)))}  correct` })) },
        { label: 'v3 per-thread counters', bars: LANGS.map((l) => ({ value: median(t1b[l].v3.map((r) => r.time_ms)), color: COLOR[l], text: `${ms(median(t1b[l].v3.map((r) => r.time_ms)))}  correct` })) },
    ],
}));

// Where one thread's time goes: I/O vs parsing, for the same 8 files on different storage.
const split = [];
const addSplit = (label, s) => { if (s?.io != null) split.push({ label, parts: [{ value: s.io, color: COLORS.io }, { value: s.cpu, color: COLORS.cpu }], text: `${ms(s.io + s.cpu)}  (I/O ${pct(s.io, s.io + s.cpu)})` }); };
for (const l of LANGS) addSplit(`${NAME[l]}, USB, cold`, at(t2cold[l], 1));
for (const l of LANGS) addSplit(`${NAME[l]}, USB, warm (cache)`, at(t2warm[l], 1));
addSplit('C#, SSD, no cache', at(t2ssdCold, 1));
addSplit('C#, SSD, warm (cache)', at(t2ssdWarm, 1));
chart('task2_split', stackedBarChart({
    title: 'Task 2, one thread: waiting for the storage vs parsing',
    subtitle: 'the same 8 files (352 MB); blue = open + read (I/O), orange = parse (CPU), ms',
    unit: 'time, ms', rows: split,
    legendItems: [{ name: 'I/O (open + read)', color: COLORS.io }, { name: 'CPU (parse)', color: COLORS.cpu }],
}));
// zoomed version without the cold USB rows, so the short bars are readable
chart('task2_split_zoom', stackedBarChart({
    title: 'The same, without the cold USB runs (zoomed in)',
    subtitle: 'when the bytes come from the cache or the SSD, parsing is the bigger part',
    unit: 'time, ms', rows: split.filter((r) => !r.label.includes('USB, cold')),
    legendItems: [{ name: 'I/O (open + read)', color: COLORS.io }, { name: 'CPU (parse)', color: COLORS.cpu }],
}));

// Speed-up of the same Task 2 code on the USB stick vs on the internal SSD.
const STORAGE = { usb: '#1baf7a', ssd: '#6250d6' };
chart('task2_storage_speedup', lineChart({
    title: 'Task 2 code (C#): speed-up on the USB stick vs on the internal SSD',
    subtitle: 'both without the file cache; time with 1 thread ÷ time with n threads',
    xs: T2SSD, yLabel: 'speed-up, ×', ideal: true, fmt: fmtX,
    series: [
        { name: 'SSD', color: STORAGE.ssd, values: speedups(t2ssdCold) },
        { name: 'USB', color: STORAGE.usb, values: [...speedups(t2cold.csharp), null] },
    ],
    yMax: Math.max(...speedups(t2ssdCold).filter(Boolean)) * 1.3,
}));

// ------------------------------------------------------------------ tables
function threadTable(data, xs, { io = false, first = 'Threads' } = {}) {
    const head = [first, ...LANGS.flatMap((l) => [`${NAME[l]} time`, `${NAME[l]} speed-up`, `${NAME[l]} peak memory`])];
    const sp = per((l) => speedups(data[l]));
    const body = xs.map((t, i) => [String(t), ...LANGS.flatMap((l) => {
        const s = data[l][i];
        return [s.time == null ? 'not measured' : (io ? sec(s.time) : ms(s.time)), x(sp[l][i]), mb(s.memory)];
    })]);
    return table(head, body, { widths: [0.8, 1.2, 1, 1.1, 1.2, 1, 1.1], align: ['center', 'right', 'right', 'right', 'right', 'right', 'right'] });
}

const totals = (() => {
    const r = rows.find((x) => x.check === 'CORRECT');
    return { neg: r.negative, pos: r.positive, zero: r.zero, total: r.total };
})();

// ------------------------------------------------------------------ document
const c1a = t1a.csharp, j1a = t1a.java;
const spA = per((l) => speedups(t1a[l]));
const spC = per((l) => speedups(t1c[l]));
const bestA = per((l) => best(t1a[l]));
const bestC = per((l) => best(t1c[l]));
const med = (list) => median(list.map((r) => r.time_ms));
const v1Totals = (l) => t1b[l].firstV1.map((r) => n0(r.total)).join(', ');

const content = [];
const add = (...items) => content.push(...items.flat());

// ---- title page (own section, see below)
const titlePage = [
    ...['MINISTRY OF EDUCATION AND RESEARCH OF THE REPUBLIC OF MOLDOVA', 'Technical University of Moldova',
        'Faculty of Computers, Informatics and Microelectronics', 'Department of Software Engineering and Automation']
        .map((t, i) => new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: i === 0 ? 120 : 40 },
                                       children: [new TextRun({ text: t, bold: i < 2, size: i === 0 ? 24 : 26 })] })),
    spacer(110),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 }, children: [new TextRun({ text: 'REPORT', bold: true, size: 40 })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: 'Laboratory work No. 1', size: 30 })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: 'Discipline: Network Programming', size: 28 })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: 'Topic: ', size: 28 }), new TextRun({ text: 'Concurrency in Practice', size: 28, bold: true })] }),
    spacer(120),
    new Paragraph({ alignment: AlignmentType.RIGHT, spacing: { after: 80 }, children: [new TextRun({ text: `Elaborated: st. gr. ${GROUP}, `, size: 26 }), new TextRun({ text: STUDENT, size: 26, bold: true })] }),
    new Paragraph({ alignment: AlignmentType.RIGHT, spacing: { after: 80 }, children: [new TextRun({ text: 'Verified: ', size: 26 }), new TextRun({ text: TEACHER, size: 26, bold: true })] }),
    spacer(100),
    new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: 'Chișinău, 2026', size: 26 })] }),
];

// ---- 1. goal + repository
add(H1('1. Goal of the work'));
add(P('Measure how the number of threads, and the way the work is divided among them, change the running time and the memory of a real workload; decide from my own measurements when the work is CPU-bound and when it is I/O-bound; observe a race condition and the cost of fixing it; and find where parallel speed-up stops.', { indent: true }));
add(P('The workload: count how many of 50,000,000 integers are negative, positive and zero. All tasks are one console program; the task and its configuration are chosen with command-line parameters. The program was written twice: in **C#** (the main version) and in **Java** (the bonus: comparison of two languages).', { indent: true }));

add(H2('1.1 Source code'));
add(new Paragraph({ spacing: { after: 120 }, children: [...runs('Git repository: '), link(REPO, REPO), ...runs(' — folder `Lab1`.')] }));
add(table(['Path in the repository', 'Contents'], [
    ['`Lab1/*.cs`, `Lab1/Lab1.csproj`', 'C# program: `Program.cs` (command dispatch), `Task1a.cs`, `Task1b.cs`, `Task1c.cs`, `Task2.cs`, shared helpers'],
    ['`Lab1/java/src/lab1/*.java`', 'Java program with the same commands, options and CSV output'],
    ['`Lab1/run_task1.ps1`, `Lab1/run_task2.ps1`', 'scripts that build both programs and run every configuration; `run_task2.ps1` also ejects the USB drive and waits for it to be re-plugged before every run'],
    ['`Lab1/results/*.csv`', 'every measurement used in this report'],
    ['`Lab1/report/`', 'the generator of this report (charts and document) from the CSV files'],
    ['`Lab1/README.md`', 'how to build and run every task, and a full explanation of the lab'],
], { widths: [1.3, 2.7] }));
add(P(''));
add(P('Build and run (from the `Lab1` folder):'));
add(code(`# C#: compile (optimised), create the data, run one configuration
dotnet build -c Release
..\\artifacts\\bin\\Lab1\\release\\Lab1.exe gen
..\\artifacts\\bin\\Lab1\\release\\Lab1.exe task1a --threads 8
# Java: compile and run the same configuration
javac -d java/out -encoding UTF-8 java/src/lab1/*.java
java --enable-native-access=ALL-UNNAMED -cp java/out lab1.Main task1a --threads 8
# every configuration of every task, both languages
powershell -ExecutionPolicy Bypass -File .\\run_task1.ps1 -Lang both -Runs 5 -Extras
powershell -ExecutionPolicy Bypass -File .\\run_task2.ps1 -Lang both -Warm`));

// ---- 2. hardware
add(H1('2. Hardware and software'));
add(table(['Component', 'Value'], [
    ['CPU', 'AMD Ryzen 9 9955HX (Zen 5), base clock 2.5 GHz, 2 chiplets × 8 cores'],
    ['Physical cores / logical processors', '**16 / 32** (SMT: 2 hardware threads per core)'],
    ['RAM', '32 GB DDR5-5600 (2 × 16 GB, Micron)'],
    ['Disk', 'Micron MTFDKBA1T0QGN, 1 TB NVMe SSD'],
    ['USB flash drive', '"VendorCo ProductCode" (controller VID 048D / PID 1234, a no-name stick), reports 124 GB; 32 GB FAT32 partition used'],
    ['USB version', '**USB 2.0 High-Speed (480 Mbit/s)**: the device descriptor says bcdUSB = 2.00 and the hub driver reports "High speed", although it was plugged into a USB 3 port. Measured: read 11.7 MB/s, write 4.5 MB/s'],
    ['Operating system', 'Windows 11 Pro 64-bit (10.0.26200), power plan "Performance", on mains power'],
    ['C#', 'C# 14, .NET SDK 10.0.401, runtime .NET 10.0.12 (x64), Release build, tiered compilation off'],
    ['Java', 'Java 25: Eclipse Temurin 25.0.5+8 LTS, HotSpot 64-bit Server VM, default G1 garbage collector'],
], { widths: [1.2, 2.8] }));
add(P(''));
add(P('The USB version was read from the USB hub driver (`IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX`, the same data the USBView tool shows), the core counts from `Win32_Processor`.', { indent: true }));

// ---- 3. data and method
add(H1('3. Data and measurement method'));
add(P(`The program's \`gen\` command writes \`numbers.txt\`: 50,000,000 random integers from −1,000,000 to 1,000,000, one per line (\`\\n\` line endings, ${n1(369454226 / 1048576)} MB), with a fixed random seed (12345) so the file is reproducible. At the same time it writes the 8 files of 6,250,000 numbers for Task 2 (the same numbers, in order, ~44 MB each) and the correct totals, which every run compares with its own result and prints CORRECT or WRONG.`, { indent: true }));
add(table(['Negative', 'Positive', 'Zero', 'Total'], [[n0(totals.neg), n0(totals.pos), n0(totals.zero), n0(totals.total)]], { align: ['center', 'center', 'center', 'center'] }));
add(P(''));
add(bullet('**Time** is measured inside the program (C#: `Stopwatch`, Java: `System.nanoTime`) from just before the threads are created until the last thread has finished. Starting the process and, in Task 1b, loading the array are not measured.'));
add(bullet('**Peak memory** is the peak working set of the process: the most physical RAM it used at any moment (Windows `PeakWorkingSetSize`; Java reads the same counter through its Foreign Function API). Every configuration runs in a new process, so peaks do not carry over between configurations.'));
add(bullet('**Task 1** reads `numbers.txt` from the Windows file cache (a warm-up run loads it first), so it measures the counting work, not the SSD. Every configuration was run **5 times**; the tables and charts show the **median**.'));
add(bullet('**Task 2** reads the 8 files from the USB drive. Before every run the script ejected the drive and waited until it was unplugged and plugged back in, so no data came from the file cache. One run per configuration.'));
add(bullet('All runs of all tasks gave the correct totals, except Task 1b version 1, which is wrong on purpose.'));

// ---- 4. Task 1
add(H1('4. Task 1: one file'));
add(H2('4.1 Task 1a: counting with threads'));
add(P('**Implementation.** The file (352 MB) is cut into N equal byte ranges, one per thread. Every thread opens the file itself and reads only its range with positional reads of 1 MB (`RandomAccess.Read` / `FileChannel.read(buffer, position)`), then parses the bytes directly (digits → number, `\'-\'` → negative, `\'\\n\'` → line finished), without creating a string per line.', { indent: true }));
add(P('**Boundaries.** A cut usually falls in the middle of a line. Rule: *a line belongs to the thread whose range contains the line\'s first byte*. A thread that does not start at 0 looks at the byte just before its range: if it is not `\\n`, the line began earlier and belongs to the previous thread, so it skips to the next `\\n`. At the end, a thread keeps reading past its range until it finishes the line it started. Every line has exactly one first byte and the ranges do not overlap, so every line is counted exactly once; the totals were correct in every run.', { indent: true }));
add(threadTable(t1a, T));
add(...figure(figures.task1a_time.png, figures.task1a_time.w, figures.task1a_time.h, 'Figure 1. Task 1a: execution time'));
add(...figure(figures.task1a_speedup.png, figures.task1a_speedup.w, figures.task1a_speedup.h, 'Figure 2. Task 1a: speed-up relative to one thread'));
add(...figure(figures.task1a_memory.png, figures.task1a_memory.w, figures.task1a_memory.h, 'Figure 3. Task 1a: peak memory'));

add(H2('4.2 Task 1b: three ways to count'));
add(P(`**Implementation.** All 50,000,000 numbers are first loaded into an \`int[]\` (not measured). The array is split into ${t1b.csharp.threads} equal parts (one per logical processor) and counted in three ways: (1) all threads increment the same three shared counters without synchronisation; (2) the same, but every increment is inside \`lock\` (C#) / \`synchronized\` (Java); (3) every thread counts into its own local counters and the main thread adds the ${t1b.csharp.threads} results after \`Join\`. Version 1 was run three times, as required; the whole measurement was repeated ${t1b.csharp.runs} times.`, { indent: true }));
const tb = (l) => [
    ...t1b[l].firstV1.map((r, i) => [NAME[l], `1. no synchronisation (run ${i + 1})`, ms(r.time_ms), n0(r.negative), n0(r.positive), n0(r.zero), n0(r.total), 'WRONG']),
    [NAME[l], `2. lock (median of ${t1b[l].v2.length})`, ms(med(t1b[l].v2)), n0(t1b[l].v2[0].negative), n0(t1b[l].v2[0].positive), n0(t1b[l].v2[0].zero), n0(t1b[l].v2[0].total), t1b[l].v2.every((r) => r.check === 'CORRECT') ? 'correct' : 'WRONG'],
    [NAME[l], `3. per-thread counters (median of ${t1b[l].v3.length})`, ms(med(t1b[l].v3)), n0(t1b[l].v3[0].negative), n0(t1b[l].v3[0].positive), n0(t1b[l].v3[0].zero), n0(t1b[l].v3[0].total), t1b[l].v3.every((r) => r.check === 'CORRECT') ? 'correct' : 'WRONG'],
];
add(table(['Lang', 'Version', 'Time', 'Negative', 'Positive', 'Zero', 'Total', 'Check'], [...tb('csharp'), ...tb('java')],
          { widths: [0.55, 2.0, 0.85, 1.05, 1.05, 0.5, 1.05, 0.8], align: ['left', 'left', 'right', 'right', 'right', 'right', 'right', 'center'] }));
add(P(''));
add(P(`Version 1 over all ${t1b.csharp.allV1.length} C# runs: ${ms(Math.min(...t1b.csharp.allV1.map((r) => r.time_ms)))} – ${ms(Math.max(...t1b.csharp.allV1.map((r) => r.time_ms)))}, totals ${n0(Math.min(...t1b.csharp.allV1.map((r) => r.total)))} – ${n0(Math.max(...t1b.csharp.allV1.map((r) => r.total)))} instead of 50,000,000. Java (${t1b.java.allV1.length} runs): totals ${n0(Math.min(...t1b.java.allV1.map((r) => r.total)))} – ${n0(Math.max(...t1b.java.allV1.map((r) => r.total)))}. The peak memory of Task 1b (C# ≈ ${mb(median(t1b.csharp.v3.map((r) => r.peak_memory_mb)))}, Java ≈ ${mb(median(t1b.java.v3.map((r) => r.peak_memory_mb)))}) is dominated by loading the array and is the same for all versions, so it is not charted.`, { indent: true }));
add(...figure(figures.task1b_time.png, figures.task1b_time.w, figures.task1b_time.h, 'Figure 4. Task 1b: time of the three versions'));

add(H2('4.3 Task 1c: producer and consumers'));
add(P('**Implementation.** One reader thread reads `numbers.txt` line by line and puts the lines, in batches of 10,000, into a blocking queue that holds at most 100 batches (C#: `BlockingCollection<List<string>>(boundedCapacity: 100)`; Java: `ArrayBlockingQueue<List<String>>(100)`). The workers repeatedly take the next batch and parse its lines (`int.Parse` / `Integer.parseInt`) into their own counters. When the queue is full, `Add`/`put` blocks the reader; when it is empty, `Take`/`take` blocks the workers. At the end of the file the reader closes the queue: C# calls `CompleteAdding()`, and the workers\' `GetConsumingEnumerable()` loop ends once the queue is empty; Java\'s queue has no "close", so the reader puts one special END batch (a "poison pill") per worker after the last real batch.', { indent: true }));
add(threadTable(t1c, T, { first: 'Workers' }));
add(...figure(figures.task1c_time.png, figures.task1c_time.w, figures.task1c_time.h, 'Figure 5. Task 1c: execution time (the x axis is the number of workers; plus 1 reader thread)'));
add(...figure(figures.task1c_speedup.png, figures.task1c_speedup.w, figures.task1c_speedup.h, 'Figure 6. Task 1c: speed-up relative to one worker'));
add(...figure(figures.task1c_memory.png, figures.task1c_memory.w, figures.task1c_memory.h, 'Figure 7. Task 1c: peak memory'));

add(H3('Extra experiment for question 6'));
const q6rows = [];
const qm = (v) => (v == null ? '–' : n0(v));
for (const l of LANGS) {
    const normal = (w) => at(t1c[l], w);
    for (const w of [1, 4]) if (normal(w)) q6rows.push([NAME[l], 'normal: 10,000 lines per batch, at most 100 batches (median)', String(w), ms(normal(w).time), mb(normal(w).memory), qm(normal(w).queueMed)]);
    for (const r of q6[l].single) q6rows.push([NAME[l], '1 line per batch, at most 100 batches', String(r.threads), ms(r.time_ms), mb(r.peak_memory_mb), qm(r.queue_max)]);
    for (const r of q6[l].unbounded) q6rows.push([NAME[l], '10,000 lines per batch, queue without a limit', String(r.threads), ms(r.time_ms), mb(r.peak_memory_mb), qm(r.queue_max)]);
}
add(table(['Lang', 'Configuration', 'Workers', 'Time', 'Peak memory', 'Max batches in queue'], q6rows,
          { widths: [0.6, 2.6, 0.75, 0.9, 1, 1], align: ['left', 'left', 'center', 'right', 'right', 'center'] }));
add(P(''));
add(P('All runs in this table gave the correct totals. "Max batches in queue" is the largest number of batches that were waiting in the queue at any moment (measured by the reader right after each `Add`/`put`).', { indent: true }));

// ---- answers to the Task 1 questions
const cA = (t) => at(t1a.csharp, t), jA = (t) => at(t1a.java, t);
const cC = (t) => at(t1c.csharp, t), jC = (t) => at(t1c.java, t);
const v1c = t1b.csharp.allV1, v1j = t1b.java.allV1;
const lost = (list) => `${(100 - (100 * Math.max(...list.map((r) => r.total))) / 50e6).toFixed(0)}–${(100 - (100 * Math.min(...list.map((r) => r.total))) / 50e6).toFixed(0)} %`;
const q6c = q6.csharp, q6j = q6.java;
const q6time = (list, w) => list.find((r) => r.threads === w)?.time_ms;
const q6mem = (list, w) => list.find((r) => r.threads === w)?.peak_memory_mb;
const q6q = (list, w) => list.find((r) => r.threads === w)?.queue_max;

add(H2('4.4 Answers to the questions of Task 1'));
add(...qa('1. At which number of threads does the speed-up stop growing? Relate it to your physical cores and logical processors.', [
    `**C#:** the speed-up is almost ideal up to 8 threads (${x(spA.csharp[1])} with 2, ${x(spA.csharp[2])} with 4, ${x(spA.csharp[3])} with 8), still grows strongly up to **16 threads = the number of physical cores** (${x(spA.csharp[4])}), grows only a little more up to **32 threads = the number of logical processors** (${x(spA.csharp[5])}, ${ms(cA(32).time)}) and stops there: 64 threads are slower again (${x(spA.csharp[6])}).`,
    'Why less than 16× with 16 threads: all threads copy their part of the file from the file cache in RAM at the same time and share the memory bandwidth; the processor lowers its clock when all 16 cores are busy (one busy core runs at a much higher boost clock); and every thread has a fixed cost (creation, opening the file, its 1 MB buffer). Why only a small gain from 16 to 32: the second logical processor of a core is SMT ("hyper-threading"): the two hardware threads share one core\'s execution units, so the second one only fills the moments when the first one waits (for example for memory) - the lecture estimates 15–30 %; here it was ' + `${((spA.csharp[5] / spA.csharp[4] - 1) * 100).toFixed(0)} %.`,
    `**Java:** the speed-up stops much earlier: the best time is with **8 threads** (${ms(jA(8).time)}, ${x(spA.java[3])}); with 16, 32 and 64 threads it is slower (${ms(jA(16).time)}, ${ms(jA(32).time)}, ${ms(jA(64).time)}). The reason is the Java runtime, not the hardware. The JVM starts every method in its interpreter, then compiles the hot loop in steps on separate compiler threads, and the final optimised code is built on assumptions from what the loop has done so far. The JVM\'s compilation log (\`-XX:+PrintCompilation\`) shows two things: the optimised loop was ready **54 ms** after start with 8 threads but only after **103 ms** with 32 threads, because the compiler threads had to compete with 32 busy workers for the 32 logical processors; and the optimised code was thrown away and rebuilt several times ("made not entrant: uncommon trap") whenever a thread took a path that had not happened before, for example reaching the end of its range. Meanwhile the threads run slower code. This warm-up does not get shorter with more threads (Amdahl\'s law), and in a run of only 100–200 ms it is a large part of the time. (The C# version is compiled to optimised code before its first run, because tiered compilation is turned off in its project file.)`,
]));
add(...qa('2. Why do 32 and 64 threads not help? Are they slower, and why?', [
    `In C#, 32 threads still help a little because they use the second hardware thread of every core (${ms(cA(16).time)} → ${ms(cA(32).time)}), but 64 threads are **slower** than 32 (${ms(cA(64).time)}). There are only 32 logical processors, so 64 threads cannot all run at once: Windows has to switch between them (a context switch saves one thread\'s registers, loads another\'s and leaves the caches full of the other thread\'s data). Every extra thread also costs something - creating and starting it (tens of microseconds), its stack, its 1 MB buffer, its own file handle - while adding no hardware to do the work. With 64 threads each part is only ${n1(369454226 / 64 / 1048576)} MB, so these fixed costs are a larger share of a run that takes only ~${n0(cA(64).time)} ms.`,
    `In Java, 32 and 64 threads are slower than 8 (${ms(jA(32).time)} and ${ms(jA(64).time)} vs ${ms(jA(8).time)}): to the costs above it adds the JIT warm-up described in question 1, which takes longer when the JVM\'s own compiler and garbage-collector threads find all the processors busy.`,
]));
add(...qa('3. Does the peak memory depend on the number of threads? Why?', [
    `**Yes, linearly.** Task 1a in C#: ${mb(cA(1).memory)} with 1 thread, ${mb(cA(64).memory)} with 64, i.e. about ${n1((cA(64).memory - cA(1).memory) / 63)} MB per extra thread - exactly the private 1 MB read buffer every thread allocates (plus a little for its stack and the runtime\'s per-thread data; a stack reserves 1 MB of address space, but only the few pages really used count in the working set). Java: ${mb(jA(1).memory)} → ${mb(jA(64).memory)}, ~${n1((jA(64).memory - jA(1).memory) / 63)} MB per thread, because \`FileChannel.read\` into a normal array goes through a temporary native buffer of the same size, and the JVM itself starts bigger.`,
    `Task 1c: the memory is set mostly by how many batches exist at the same time - those waiting in the queue plus one being counted by every worker - so it also grows with the workers in C# (${mb(cC(1).memory)} → ${mb(cC(64).memory)}). In Java it stays around ${mb(jC(4).memory)}: there the garbage collector\'s heap sizing dominates - the JVM lets the heap grow to hundreds of MB before collecting the 50 million short-lived strings. Task 1b uses the same ~${n0(median(t1b.csharp.v3.map((r) => r.peak_memory_mb)))} MB (C#) for every version: the 200 MB array and the leftovers of loading it dominate, and its threads allocate nothing.`,
]));
add(...qa('4. Rank the three versions of Task 1b by time. Which of them are correct? Explain each result.', [
    `**Fastest to slowest: version 3 (per-thread counters) < version 1 (no synchronisation) < version 2 (lock).** C#: ${ms(med(t1b.csharp.v3))} < ${ms(Math.min(...v1c.map((r) => r.time_ms)))}–${ms(Math.max(...v1c.map((r) => r.time_ms)))} < ${sec(med(t1b.csharp.v2))}. Java: ${ms(med(t1b.java.v3))} < ${ms(Math.min(...v1j.map((r) => r.time_ms)))}–${ms(Math.max(...v1j.map((r) => r.time_ms)))} < ${sec(med(t1b.java.v2))}. **Versions 2 and 3 are correct; version 1 is wrong in every run** and gives a different total each time.`,
    `**Version 1** has a race condition. \`counter++\` is three steps: read the value into a register, add 1, write it back. When two threads read the same old value, both write old + 1, and one of the two increments is lost. Here ${lost(v1c)} of the increments were lost in C# and ${lost(v1j)} in Java. It is also slow, although it never waits for a lock: the three counters sit in the same 64-byte cache line, and every write needs that line exclusively in the writing core\'s cache, so the line keeps moving between the 32 logical processors (cache-coherence traffic). Each increment then costs a trip between cores instead of one cycle.`,
    `**Version 2** is correct because only one thread at a time can be inside the \`lock\`/\`synchronized\` block, so the read-add-write steps of two threads can no longer interleave. It is the slowest because it takes and releases the lock 50,000,000 times, with 32 threads fighting for it: almost all the time is spent waiting, and the counting is done one thread at a time - slower than a single thread without a lock. (Java\'s lock was about ${(med(t1b.csharp.v2) / med(t1b.java.v2)).toFixed(1)}× faster than C#\'s under this extreme contention, but both are far slower than versions 1 and 3.)`,
    `**Version 3** is correct and fastest: every thread counts its part into its own local variables, which the compiler keeps in registers; nothing is shared during the loop, so there is no race and no waiting. The ${t1b.csharp.threads} partial results are added once, after \`Join\` guarantees that all threads have finished. In C# it reads the 200 MB array in ${ms(med(t1b.csharp.v3))} (≈ ${n0(200 / (med(t1b.csharp.v3) / 1000) / 1000)} GB/s): the memory bandwidth is the limit. Java\'s ${ms(med(t1b.java.v3))} is mostly the JIT warm-up described in question 1.`,
]));
add(...qa('5. Compare Task 1c with Task 1a. Which one is faster, and why? At which number of workers does Task 1c stop getting faster?', [
    `**Task 1a is much faster:** best C# ${ms(bestA.csharp.time)} vs ${ms(bestC.csharp.time)} for Task 1c (${(bestC.csharp.time / bestA.csharp.time).toFixed(0)}×); Java ${ms(bestA.java.time)} vs ${ms(bestC.java.time)}. In Task 1c **one** reader thread does a sequential job for the whole file: it decodes the bytes into characters, finds the line ends and creates 50,000,000 string objects and 5,000 lists. That part cannot be shared between threads, and it alone takes about one second, so it limits the whole task however many workers there are (Amdahl\'s law: the serial part sets the minimum time). In Task 1a every thread reads and parses its own part of the raw bytes, in parallel and without creating any strings.`,
    `**Where Task 1c stops getting faster.** C#: ${ms(cC(1).time)} with 1 worker, best with **2 workers** (${ms(cC(2).time)}), and from 4 workers it gets slower again (${ms(cC(8).time)} with 8, ${ms(cC(64).time)} with 64). Java: about ${ms(jC(1).time)} already with **1 worker** and the same with 64. The queue measurement shows why: with 1 C# worker the queue held up to ${n0(cC(1).queueMax)} batches (the reader was sometimes ahead and waited for the worker), but from 2 workers on it never held more than ${n0(Math.max(...[2, 4, 8, 16, 32, 64].map((w) => cC(w).queueMax)))} - the workers were waiting for the reader. In Java the queue held at most ${n0(jC(1).queueMax)} batches even with 1 worker: one Java worker is already faster than the reader. (With 8 or more Java workers the queue sometimes filled up for a moment - in some runs to the limit, in others not at all - when the workers were stalled together, for example by a garbage-collection pause; the total time hardly changed.) Extra workers only add cost: they compete for the queue\'s lock, are woken up and put to sleep more often, and allocate from more threads, which makes the garbage collector\'s work more expensive.`,
]));
add(...qa('6. What would happen with batches of a single line? And with a queue that has no size limit?', [
    `**Batches of one line** (measured): C# ${sec(q6time(q6c.single, 1))} with 1 worker and ${sec(q6time(q6c.single, 4))} with 4 workers; Java ${sec(q6time(q6j.single, 1))} and ${sec(q6time(q6j.single, 4))}, against ${sec(Math.min(cC(1).time, jC(1).time))}–${sec(Math.max(cC(1).time, jC(1).time))} with normal batches. Every batch costs one \`Add\` and one \`Take\`, each of which takes the queue\'s lock and may have to wake a sleeping thread through the operating system. With batches of 10,000 lines that cost is paid 5,000 times; with single lines it is paid 50,000,000 times, and it is far bigger than the work per line (parsing one number takes nanoseconds). More workers make it even slower, because they all fight for the same queue lock. Batching exists to spread the synchronisation cost over many items.`,
    `**A queue without a size limit** removes the back-pressure: the reader never waits. If the workers are slower than the reader, the batches pile up in memory - in the worst case the whole file as 50,000,000 strings, several gigabytes - and the program can run out of memory; the limit of 100 batches keeps at most ~1,000,000 lines in the queue. In this measurement the workers were nearly as fast as the reader, so the queue never even reached the old limit of 100 and removing it changed nothing: C# with 1 worker held up to ${n0(q6q(q6c.unbounded, 1))} batches (${mb(q6mem(q6c.unbounded, 1))}), with 4 workers ${n0(q6q(q6c.unbounded, 4))} (${mb(q6mem(q6c.unbounded, 4))}); Java with 1 worker ${n0(q6q(q6j.unbounded, 1))} batches. The limit matters when the consumers are slower than the producer - for example when each item needs real processing or a network call.`,
]));

// ---- Task 2
add(H1('5. Task 2: many files on slow storage'));
add(P('**Setup.** The 8 files (6,250,000 numbers each, 352 MB in total) were copied to the folder `lab1_task2` on the USB drive; copying took 78 s (4.5 MB/s). The script `run_task2.ps1` then, before every run: ejected the drive from code (`CM_Request_Device_Eject`, the same as "Safely Remove Hardware"), waited until it was pulled out, waited until it was plugged back in and had stayed connected for 8 seconds, and only then started the program. Ejecting and re-plugging dismounts the drive, so Windows forgets everything it had cached from it, and every measured run read all 352 MB from the flash memory.', { indent: true }));
add(P('**Implementation.** A file is the unit of work; the 8 files are divided among the threads in equal consecutive groups (1 thread: all 8; 2 threads: 4 each; 4 threads: 2 each). For each file a thread opens it, reads it with **one** read call into **one** byte buffer (C#: a reused `byte[]`; Java: a reused direct `ByteBuffer`), and parses the bytes with the same parser as Task 1a. Each thread also measures how long it spent opening and reading (I/O) and how long it spent parsing (CPU).', { indent: true }));
const t2tab = [];
for (const l of LANGS) for (const t of T2) {
    const s = at(t2cold[l], t), w = at(t2warm[l], t), base = at(t2cold[l], 1)?.time;
    t2tab.push([NAME[l], String(t), s?.time ? sec(s.time) : 'not completed*', s?.time && base ? x(base / s.time) : '–', mb(s?.memory), s?.io ? sec(s.io) : '–', s?.cpu ? ms(s.cpu) : '–', w?.time ? ms(w.time) : '–']);
}
add(table(['Lang', 'Threads', 'Time (USB, cold)', 'Speed-up', 'Peak memory', 'I/O, sum of threads', 'Parse, sum of threads', 'Same, from cache'], t2tab,
          { widths: [0.6, 0.65, 1.05, 0.8, 0.95, 1.05, 1.05, 1.0], align: ['left', 'center', 'right', 'right', 'right', 'right', 'right', 'right'] }));
add(P(''));
const missingJava = T2.filter((t) => !at(t2cold.java, t)?.time);
add(P(`All completed runs gave the correct totals. "Same, from cache" is an extra run started right after the measured one, without re-plugging: the files then come from the Windows file cache in RAM. **About the drive:** during the session Windows logged 1,341 read errors from this no-name stick (System log, source "disk", event 51); Windows repeats a failed read, so these only cost time, but five runs - all with 2 or more threads reading at once - failed with "A device which does not exist was specified" because the stick reset itself and disconnected. Such a run produced no result; it was repeated after a new re-plug. ${missingJava.length ? `*The Java run with 2 threads failed this way in all four attempts, so the Java runs with ${missingJava.join(' and ')} threads could not be completed with this stick. The required part of Task 2 (C#, 1, 2 and 4 threads) is complete.` : ''}`, { indent: true }));
add(...figure(figures.task2_time.png, figures.task2_time.w, figures.task2_time.h, 'Figure 8. Task 2: execution time with the files on the USB drive'));
add(...figure(figures.task2_speedup.png, figures.task2_speedup.w, figures.task2_speedup.h, 'Figure 9. Task 2: speed-up relative to one thread'));
add(...figure(figures.task2_memory.png, figures.task2_memory.w, figures.task2_memory.h, 'Figure 10. Task 2: peak memory'));
add(...figure(figures.task2_split.png, figures.task2_split.w, figures.task2_split.h, 'Figure 11. Task 2 with one thread: time spent waiting for the storage (I/O) and parsing (CPU)'));
add(...figure(figures.task2_split_zoom.png, figures.task2_split_zoom.w, figures.task2_split_zoom.h, 'Figure 12. The same without the cold USB runs'));
add(P(`**Extra experiment: the same Task 2 code on the internal SSD.** With the option \`--cache off\` the C# program opens the files with \`FILE_FLAG_NO_BUFFERING\`, which makes Windows read straight from the device instead of the file cache - a cold read without unplugging anything. On the NVMe SSD (median of 3 runs) one thread took ${ms(at(t2ssdCold, 1)?.time)}, of which only ${ms(at(t2ssdCold, 1)?.io)} was I/O; ${T2SSD.slice(1).map((t) => `${t} threads: ${ms(at(t2ssdCold, t)?.time)}`).join(', ')}. The speed-up is not ideal because there are only 8 files, and with 8 threads the SSD\'s bandwidth starts to matter, but more threads clearly help.`, { indent: true }));
add(...figure(figures.task2_storage_speedup.png, figures.task2_storage_speedup.w, figures.task2_storage_speedup.h, 'Figure 13. The same program and files: more threads help on the SSD, not on the USB drive'));

const u1 = at(t2cold.csharp, 1), uj1 = at(t2cold.java, 1);
add(H2('5.1 Answers to the questions of Task 2'));
add(...qa('1. Compare the time on one thread with Task 1a on one thread. On the USB drive, is the work CPU-bound or I/O-bound?', [
    `Task 1a with one thread parses all 50,000,000 numbers in ${ms(cA(1).time)} (C#). Task 2 with one thread parses **the same numbers** and needs **${sec(u1.time)}**, ${(u1.time / cA(1).time).toFixed(0)}× longer. The thread\'s own measurement shows where the time goes: ${sec(u1.io)} (${pct(u1.io, u1.io + u1.cpu)}) waiting for the drive to deliver the bytes and only ${ms(u1.cpu)} parsing - the same amount of CPU work as in Task 1a. The drive delivered ${n1(352.3 / (u1.time / 1000))} MB/s, which is what a cheap USB 2.0 stick can do.`,
    `Two more measurements confirm it. Reading the same files again from the file cache took ${ms(at(t2warm.csharp, 1)?.time)} instead of ${sec(u1.time)} (${(u1.time / at(t2warm.csharp, 1)?.time).toFixed(0)}× faster) - removing the waiting removes almost all the time. And the language does not matter: Java parses more slowly (${ms(uj1?.cpu)} vs ${ms(u1.cpu)}) but needed the same ${sec(uj1?.time)}, because both wait for the same drive. **On the USB drive the work is I/O-bound.**`,
]));
add(...qa('2. On the USB drive, do more threads help? Why not, when lecture 1 showed that I/O-bound work benefits from concurrency?', [
    `**No.** C#: ${T2.map((t) => `${t} thread${t > 1 ? 's' : ''}: ${sec(at(t2cold.csharp, t).time)}`).join(', ')} - the same time within 1–2 %, while the peak memory grew from ${mb(at(t2cold.csharp, 1).memory)} to ${mb(at(t2cold.csharp, 4).memory)} because every thread holds a ~44 MB buffer. The sum of the threads\' I/O time grew to ${sec(at(t2cold.csharp, 2).io)} with 2 threads and ${sec(at(t2cold.csharp, 4).io)} with 4: the threads did not read faster, they only waited **together**.`,
    'Concurrency helps I/O-bound work when the waits are **independent** and can overlap - for example a server waiting for answers from many different machines: each remote machine works on its own request, so ten requests in parallel take about as long as one. Here every thread waits for the **same single device**. The drive has a fixed speed (~11.7 MB/s); a USB 2.0 stick handles one command at a time, so requests from 4 threads simply queue up behind each other, and 352 MB at 11.7 MB/s take ~30 s however the requests are split. In the lecture\'s words: more people do not make one pizza oven bake faster. Parallel requests could even be slower, because the drive then jumps between 4 files instead of reading one file from start to end.',
    `On the internal SSD the same code does get faster with more threads (Figure 13): the SSD is so fast (${n0(352.3 / ((at(t2ssdCold, 1)?.io ?? 1) / 1000))} MB/s for one thread) that parsing dominates, i.e. the same program is CPU-bound there.`,
]));

// ---- Bonus: C# vs Java
add(H1('6. Bonus: C# compared with Java'));
add(P('Both versions implement the same algorithms with the same data, options and output, so the differences below come from the languages and their runtimes.', { indent: true }));
add(H2('6.1 How threads, locks and queues are written'));
add(table(['', 'C# (.NET 10)', 'Java 25'], [
    ['Create and wait for an OS thread', '`var t = new Thread(() => Work()); t.Start(); t.Join();`', '`Thread t = new Thread(() -> work()); t.start(); t.join();`'],
    ['Loop variable in a lambda', 'compiles, but all iterations share one `for` variable → copy it (`int index = t;`)', 'does not compile unless the variable is effectively final → the same copy is forced'],
    ['Lock', '`lock (gate) { ... }` with a `System.Threading.Lock` object', '`synchronized (gate) { ... }` on any object (or `ReentrantLock`)'],
    ['Bounded blocking queue', '`BlockingCollection<T>(boundedCapacity: 100)`; `Add` / `GetConsumingEnumerable()`', '`ArrayBlockingQueue<T>(100)`; `put` / `take`'],
    ['Closing the queue', '`CompleteAdding()`: consumers stop when it is empty', 'no "close": a poison-pill object, one per consumer'],
    ['Per-thread result', '`struct SignCounts` (value type, copied)', '`class SignCounts` (reference type, one object per thread)'],
    ['Read at a file offset', '`RandomAccess.Read(handle, span, offset)`', '`FileChannel.read(buffer, position)`'],
    ['Peak memory', '`Process.GetCurrentProcess().PeakWorkingSet64`', 'no API: call `K32GetProcessMemoryInfo` from kernel32.dll with the Foreign Function API'],
    ['Exceptions in threads', 'unhandled → the process stops', 'printed and the thread dies; the program must collect them itself'],
], { widths: [1.1, 1.6, 1.6] }));
add(H2('6.2 Do the threads really run in parallel?'));
add(P(`Yes, in both languages. \`new Thread\` creates a real operating-system ("platform") thread in .NET and in Java, and neither runtime has a global lock like CPython\'s GIL, so the threads run on different cores at the same time. The measurements prove it: Task 1a runs ${x(spA.csharp[5])} faster with 32 threads in C# and ${x(spA.java[3])} faster with 8 threads in Java, which is impossible if only one thread runs at a time; and the lost updates of Task 1b version 1 (${lost(v1c)} in C#, ${lost(v1j)} in Java) need truly simultaneous increments. (Java 21+ also has *virtual threads*, which are not OS threads; they were not used, as the lab requires OS threads.)`, { indent: true }));
add(H2('6.3 Time and memory of the same tasks'));
add(table(['Measurement', 'C#', 'Java'], [
    ['Task 1a, 1 thread', `${ms(cA(1).time)}, ${mb(cA(1).memory)}`, `${ms(jA(1).time)}, ${mb(jA(1).memory)}`],
    ['Task 1a, best', `${ms(bestA.csharp.time)} with ${bestA.csharp.threads} threads (${x(spA.csharp[T.indexOf(bestA.csharp.threads)])})`, `${ms(bestA.java.time)} with ${bestA.java.threads} threads (${x(spA.java[T.indexOf(bestA.java.threads)])})`],
    ['Task 1b v1 (no sync), lost updates', lost(v1c), lost(v1j)],
    ['Task 1b v2 (lock)', sec(med(t1b.csharp.v2)), sec(med(t1b.java.v2))],
    ['Task 1b v3 (per-thread)', ms(med(t1b.csharp.v3)), ms(med(t1b.java.v3))],
    ['Task 1b peak memory', mb(median(t1b.csharp.v3.map((r) => r.peak_memory_mb))), mb(median(t1b.java.v3.map((r) => r.peak_memory_mb)))],
    ['Task 1c, best', `${ms(bestC.csharp.time)} with ${bestC.csharp.threads} worker${bestC.csharp.threads > 1 ? 's' : ''}`, `${ms(bestC.java.time)} with ${bestC.java.threads} worker${bestC.java.threads > 1 ? 's' : ''}`],
    ['Task 1c peak memory (4 workers)', mb(cC(4).memory), mb(jC(4).memory)],
    ['Task 2, USB, 1 thread', `${sec(u1.time)} (parse ${ms(u1.cpu)})`, `${sec(uj1?.time)} (parse ${ms(uj1?.cpu)})`],
    ['Task 2, USB, 1 thread, peak memory', mb(u1.memory), mb(uj1?.memory)],
], { widths: [1.5, 1.5, 1.5] }));
add(P(''));
add(H2('6.4 What the comparison shows'));
add(bullet(`**Start-up and warm-up: C# wins short runs.** The C# build compiles every method to optimised machine code before its first call (tiered compilation off); the JVM starts in the interpreter and compiles hot code while the program runs. For runs of 10–200 ms this warm-up is a large part of the time (Task 1b v3: ${ms(med(t1b.csharp.v3))} vs ${ms(med(t1b.java.v3))}; Task 1a: ${ms(bestA.csharp.time)} vs ${ms(bestA.java.time)}), and because it does not shrink with more threads, Java stops scaling earlier.`));
add(bullet(`**Memory: Java needs more.** An empty JVM already uses more memory than the .NET runtime (Task 1a with 1 thread: ${mb(jA(1).memory)} vs ${mb(cA(1).memory)}), and its garbage collector lets the heap grow much more before collecting (Task 1c: ~${n0(jC(4).memory)} MB vs ~${n0(cC(4).memory)} MB).`));
add(bullet(`**Strings and the producer–consumer task: Java is faster.** Java stores text like "-42" with one byte per character (compact strings), .NET with two (UTF-16), so Java\'s reader creates less data and its Task 1c is faster and does not slow down with more workers.`));
add(bullet(`**Heavy lock contention: Java\'s synchronized was ${(med(t1b.csharp.v2) / med(t1b.java.v2)).toFixed(1)}× faster** than C#\'s \`lock\` with 32 threads fighting for one lock - but both are the wrong tool here; per-thread counters are hundreds of times faster in both languages.`));
add(bullet('**I/O-bound work: no difference.** On the USB drive both languages take the same ~30 s, because both wait for the same device.'));

// ---- conclusion
add(H1('7. Conclusion'));
add(P(`**CPU-bound on my machine:** Tasks 1a, 1b and 1c, whose input was already in RAM (file cache), and Task 2 when the files are on the internal NVMe SSD. I know because more threads made them faster - Task 1a up to ${x(spA.csharp[5])} with 32 threads - and because, when Task 2 reads from the cache or the SSD, parsing takes most of the time. Their speed-up stopped where the hardware stops: almost linear up to the 16 physical cores, a small SMT gain up to the 32 logical processors, and a loss beyond that. Task 1c is a special case: it is limited by its single reader thread (a serial part), so it stops improving after 2 workers in C# and does not improve at all in Java.`, { indent: true }));
add(P(`**I/O-bound on my machine:** Task 2 with the files on the USB 2.0 flash drive. I know because ${pct(u1.io, u1.io + u1.cpu)} of the time was spent waiting for the drive and only ${ms(u1.cpu)} parsing; because 1, 2 and 4 threads all took ~30 s; because the same files from the cache took ${ms(at(t2warm.csharp, 1)?.time)}; and because C# and Java, which parse at different speeds, took the same time. More threads do not help I/O-bound work that waits for one single slow device; they only cost memory.`, { indent: true }));
add(P('The other lessons: shared counters without synchronisation lose most of the updates; a lock makes them correct but serialises the threads and is the slowest option; giving every thread its own data and combining the results at the end is both correct and the fastest. The cost of synchronisation must be spread over enough work (batches), and a bounded queue protects memory when the producer is faster than the consumers.', { indent: true }));

// ------------------------------------------------------------------ save
const doc = new Document({
    creator: STUDENT,
    title: 'Laboratory work No. 1 - Concurrency in Practice',
    description: 'Network Programming, Lab 1 report',
    numbering: numberingConfig,
    styles: {
        default: { document: { run: { font: FONT, size: 24 } } },
        paragraphStyles: [
            { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
              run: { font: FONT, size: 30, bold: true, color: '000000' }, paragraph: { spacing: { before: 360, after: 160 }, keepNext: true, outlineLevel: 0 } },
            { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
              run: { font: FONT, size: 26, bold: true, color: '000000' }, paragraph: { spacing: { before: 240, after: 120 }, keepNext: true, outlineLevel: 1 } },
            { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true,
              run: { font: FONT, size: 24, bold: true, italics: true, color: '000000' }, paragraph: { spacing: { before: 200, after: 100 }, keepNext: true, outlineLevel: 2 } },
        ],
    },
    sections: [
        {
            properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1701, right: 850 } } },
            children: titlePage,
        },
        {
            properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1701, right: 850 },
                                   pageNumbers: { start: 2 } } },
            footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT], size: 20 })] })] }) },
            children: content,
        },
    ],
});

const outFile = path.join(here, `Lab1_Report_Eftode_Andrei_${GROUP}.docx`);
fs.writeFileSync(outFile, await Packer.toBuffer(doc));
console.log(`Report written to ${outFile}`);

// ------------------------------------------------------------------ summary.md (for the README)
const mdRow = (cells) => `| ${cells.join(' | ')} |`;
const mdTable = (data, xs, unit = ms) => [
    mdRow(['Threads', ...LANGS.flatMap((l) => [`${NAME[l]} time`, `${NAME[l]} speed-up`, `${NAME[l]} peak memory`])]),
    mdRow(Array(1 + 3 * LANGS.length).fill('---:')),
    ...xs.map((t, i) => mdRow([t, ...LANGS.flatMap((l) => { const s = data[l][i]; const sp = speedups(data[l])[i]; return [s.time == null ? 'not completed' : unit(s.time), x(sp), mb(s.memory)]; })])),
].join('\n');
const summary = `# Measured results (medians)

Generated by report/build_report.mjs from results/*.csv.

## Task 1a
${mdTable(t1a, T)}

## Task 1b (${t1b.csharp.threads} threads)
| Version | C# time | C# total | Java time | Java total |
|---|---:|---:|---:|---:|
${[0, 1, 2].map((i) => `| v1 no sync, run ${i + 1} | ${ms(t1b.csharp.firstV1[i]?.time_ms)} | ${n0(t1b.csharp.firstV1[i]?.total)} (WRONG) | ${ms(t1b.java.firstV1[i]?.time_ms)} | ${n0(t1b.java.firstV1[i]?.total)} (WRONG) |`).join('\n')}
| v2 lock (median) | ${ms(med(t1b.csharp.v2))} | 50,000,000 | ${ms(med(t1b.java.v2))} | 50,000,000 |
| v3 per-thread (median) | ${ms(med(t1b.csharp.v3))} | 50,000,000 | ${ms(med(t1b.java.v3))} | 50,000,000 |

## Task 1c (x = workers)
${mdTable(t1c, T)}

Largest number of batches waiting in the queue (max over runs): C# ${T.map((t) => `${t}:${cC(t).queueMax}`).join(', ')}; Java ${T.map((t) => `${t}:${jC(t).queueMax}`).join(', ')}

Question 6: batches of 1 line - C# ${sec(q6time(q6c.single, 1))} (1 worker), ${sec(q6time(q6c.single, 4))} (4 workers); Java ${sec(q6time(q6j.single, 1))}, ${sec(q6time(q6j.single, 4))}. Unbounded queue - C# 1 worker ${ms(q6time(q6c.unbounded, 1))}, ${mb(q6mem(q6c.unbounded, 1))}, max ${q6q(q6c.unbounded, 1)} batches; 4 workers ${ms(q6time(q6c.unbounded, 4))}, ${mb(q6mem(q6c.unbounded, 4))}; Java 1 worker ${ms(q6time(q6j.unbounded, 1))}, ${mb(q6mem(q6j.unbounded, 1))}, max ${q6q(q6j.unbounded, 1)} batches.

## Task 2 (USB drive, re-plugged before every run)
${mdTable(t2cold, T2, sec)}

| | C# 1 thread | Java 1 thread |
|---|---:|---:|
| I/O (open + read) | ${sec(u1.io)} | ${sec(uj1?.io)} |
| CPU (parse) | ${ms(u1.cpu)} | ${ms(uj1?.cpu)} |
| same files from the cache | ${ms(at(t2warm.csharp, 1)?.time)} | ${ms(at(t2warm.java, 1)?.time)} |

Task 2 code on the internal SSD without the cache (C#): ${T2SSD.map((t) => `${t} thr: ${ms(at(t2ssdCold, t)?.time)} (I/O ${ms(at(t2ssdCold, t)?.io)}, parse ${ms(at(t2ssdCold, t)?.cpu)})`).join('; ')}
`;
fs.writeFileSync(path.join(resultsDir, 'summary.md'), summary);
console.log('Summary written to results/summary.md');
