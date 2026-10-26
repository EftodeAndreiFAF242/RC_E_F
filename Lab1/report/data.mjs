// Reads every results/*.csv written by the C# and Java programs and reduces repeated runs to their median.
import fs from 'node:fs';
import path from 'node:path';

const NUMERIC = ['threads', 'time_ms', 'peak_memory_mb', 'negative', 'positive', 'zero', 'total', 'io_ms', 'cpu_ms', 'queue_max'];

/** All rows of all CSV files in `dir` that use the current format (first column = lang). */
export function loadRows(dir) {
    const rows = [];
    for (const file of fs.readdirSync(dir).filter((f) => f.endsWith('.csv')).sort()) {
        const lines = fs.readFileSync(path.join(dir, file), 'utf8').split(/\r?\n/).filter((l) => l.trim() !== '');
        const header = lines[0].split(',');
        if (header[0] !== 'lang') continue;                       // an older file format: skip
        lines.slice(1).forEach((line, index) => {
            const cells = line.split(',');
            const row = Object.fromEntries(header.map((h, i) => [h, cells[i] ?? '']));
            // An empty cell, or a column that an older file does not have, means "not measured" (null).
            for (const key of NUMERIC) row[key] = row[key] == null || row[key] === '' ? null : Number(row[key]);
            row.file = file;
            row.order = index;                                    // position inside its file
            rows.push(row);
        });
    }
    return rows;
}

export const median = (values) => {
    const s = values.filter((v) => v != null).sort((a, b) => a - b);
    if (s.length === 0) return null;
    const m = s.length >> 1;
    return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2;
};

/**
 * Groups rows by thread count and returns, per thread count, the median time and memory,
 * how many runs there were, and whether every run was CORRECT.
 */
export function summarize(rows, threadCounts) {
    return threadCounts.map((t) => {
        const runs = rows.filter((r) => r.threads === t);
        return {
            threads: t,
            runs: runs.length,
            time: median(runs.map((r) => r.time_ms)),
            minTime: runs.length ? Math.min(...runs.map((r) => r.time_ms)) : null,
            maxTime: runs.length ? Math.max(...runs.map((r) => r.time_ms)) : null,
            memory: median(runs.map((r) => r.peak_memory_mb)),
            io: median(runs.map((r) => r.io_ms)),
            cpu: median(runs.map((r) => r.cpu_ms)),
            queueMax: runs.some((r) => r.queue_max != null) ? Math.max(...runs.map((r) => r.queue_max ?? 0)) : null,
            queueMed: median(runs.map((r) => r.queue_max)),
            allCorrect: runs.length > 0 && runs.every((r) => r.check === 'CORRECT'),
            row: runs[0],
        };
    });
}

export function speedups(summary) {
    const base = summary[0]?.time;
    return summary.map((s) => (s.time && base ? base / s.time : null));
}

export function pick(rows, filter) {
    return rows.filter((r) => Object.entries(filter).every(([k, v]) => r[k] === v));
}
