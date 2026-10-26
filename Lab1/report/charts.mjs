// =============================================================================
//  Draws the report's charts as SVG (a text format for vector pictures) and turns
//  them into PNG images with resvg. No chart library: each chart is a few dozen
//  SVG elements, so every pixel is under our control.
//
//  Look: thin 2px lines, 8px dots with a white ring, solid hairline grid,
//  C# = blue, Java = orange (checked for colour-blind safety), text in gray/black,
//  never in the series colour. Layout: title, subtitle, legend row, then the plot.
// =============================================================================
import { Resvg } from '@resvg/resvg-js';

export const COLORS = {
    surface: '#fcfcfb',
    ink: '#0b0b0b',
    ink2: '#52514e',
    muted: '#898781',
    grid: '#e1e0d9',
    axis: '#c3c2b7',
    csharp: '#2a78d6',   // categorical slot 1 (blue)
    java: '#eb6834',     // categorical slot 2 (orange)
    io: '#2a78d6',       // Task 2 split: waiting for the drive
    cpu: '#eb6834',      //               parsing
};

const FONT = "'Segoe UI', Arial, sans-serif";
const W = 720;           // SVG width in "pixels" (the PNG is rendered 3x bigger for print)

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function text(x, y, s, { size = 12, fill = COLORS.ink2, anchor = 'start', weight = 400, rotate } = {}) {
    const tr = rotate ? ` transform="rotate(${rotate} ${x} ${y})"` : '';
    return `<text x="${x}" y="${y}"${tr} font-family="${FONT}" font-size="${size}" fill="${fill}" ` +
           `text-anchor="${anchor}" font-weight="${weight}">${esc(s)}</text>`;
}

/** Picks a "nice" axis maximum and step (1, 2, 2.5, 5 x 10^k) so ticks are round numbers. */
function niceScale(maxValue, targetTicks = 5) {
    if (maxValue <= 0) return { max: 1, step: 0.2 };
    const rough = maxValue / targetTicks;
    const pow = Math.pow(10, Math.floor(Math.log10(rough)));
    const step = [1, 2, 2.5, 5, 10].map((m) => m * pow).find((s) => s >= rough);
    return { max: Math.ceil(maxValue / step - 1e-9) * step, step };
}

/** Axis tick text: 0 / 500 / 1,000 / 2.5 ... */
export function formatNumber(v) {
    if (Math.abs(v) >= 100) return Math.round(v).toLocaleString('en-US');
    if (Math.abs(v) >= 10 || v % 1 === 0) return v.toFixed(0);
    return String(Number(v.toFixed(2)));
}

/** Header: title, subtitle and (for 2+ series) a legend row. Returns the SVG and where the plot may start. */
function header(title, subtitle, legendItems, shape = 'line') {
    let svg = text(16, 26, title, { size: 15, fill: COLORS.ink, weight: 600 });
    if (subtitle) svg += text(16, 45, subtitle, { size: 11.5, fill: COLORS.muted });
    if (!legendItems || legendItems.length < 2) return { svg, top: 78 };
    let x = 16;
    for (const item of legendItems) {
        if (shape === 'square') {
            svg += `<rect x="${x}" y="58" width="12" height="12" rx="2" fill="${item.color}"/>`;
        } else {
            svg += `<line x1="${x}" y1="64" x2="${x + 18}" y2="64" stroke="${item.color}" stroke-width="2" stroke-linecap="round"/>`;
            svg += `<circle cx="${x + 9}" cy="64" r="4" fill="${item.color}" stroke="${COLORS.surface}" stroke-width="2"/>`;
        }
        svg += text(x + (shape === 'square' ? 18 : 25), 68, item.name, { size: 12, fill: COLORS.ink2 });
        x += (shape === 'square' ? 18 : 25) + item.name.length * 6.6 + 22;
    }
    return { svg, top: 100 };
}

/**
 * Line chart against thread counts (x positions evenly spaced: 1, 2, 4, ... 64).
 * series: [{ name, color, values: [number|null per x] }]
 * ideal: also draw the ideal speed-up line y = x; refLines: [{ x, label }]
 * fmt: how values are written in the end labels
 */
export function lineChart({ title, subtitle, xs, series, yLabel, xLabel = 'number of threads', ideal = false,
                            refLines = [], yMax, labelEnds = true, fmt = formatNumber }) {
    const H = 350, left = 64, right = 100, bottom = 48;
    const head = header(title, subtitle, series);
    const top = head.top + (refLines.length ? 10 : 0);
    const pw = W - left - right, ph = H - top - bottom;
    const all = series.flatMap((s) => s.values.filter((v) => v != null));
    const { max, step } = niceScale(yMax ?? Math.max(...all) * 1.08);
    const xAt = (i) => left + (xs.length === 1 ? pw / 2 : (i * pw) / (xs.length - 1));
    const yAt = (v) => top + ph - (v / max) * ph;

    let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">`;
    svg += `<rect width="${W}" height="${H}" fill="${COLORS.surface}"/>` + head.svg;

    // horizontal grid + y ticks
    for (let v = 0; v <= max + 1e-9; v += step) {
        const y = yAt(v);
        svg += `<line x1="${left}" y1="${y}" x2="${left + pw}" y2="${y}" stroke="${v === 0 ? COLORS.axis : COLORS.grid}" stroke-width="1"/>`;
        svg += text(left - 8, y + 4, formatNumber(v), { size: 11, fill: COLORS.muted, anchor: 'end' });
    }
    svg += text(18, top + ph / 2, yLabel, { size: 11, fill: COLORS.muted, anchor: 'middle', rotate: -90 });

    xs.forEach((x, i) => { svg += text(xAt(i), top + ph + 18, x, { size: 11, fill: COLORS.muted, anchor: 'middle' }); });
    svg += text(left + pw / 2, H - 8, xLabel, { size: 11, fill: COLORS.muted, anchor: 'middle' });

    // reference lines (physical cores / logical processors)
    for (const r of refLines) {
        const i = xs.indexOf(r.x);
        if (i < 0) continue;
        svg += `<line x1="${xAt(i)}" y1="${top - 4}" x2="${xAt(i)}" y2="${top + ph}" stroke="${COLORS.axis}" stroke-width="1"/>`;
        svg += text(xAt(i) + (r.anchor === 'end' ? -4 : 4), top - 7, r.label, { size: 10.5, fill: COLORS.muted, anchor: r.anchor ?? 'start' });
    }

    // ideal speed-up y = x, stopped where it leaves the plot
    if (ideal) {
        const pts = [];
        for (let i = 0; i < xs.length; i++) {
            if (xs[i] <= max) { pts.push([xAt(i), yAt(xs[i])]); continue; }
            const f = (max - xs[i - 1]) / (xs[i] - xs[i - 1]);
            pts.push([xAt(i - 1) + f * (xAt(i) - xAt(i - 1)), yAt(max)]);
            break;
        }
        svg += `<path d="${pts.map((p, k) => `${k ? 'L' : 'M'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ')}" fill="none" stroke="${COLORS.axis}" stroke-width="1.5"/>`;
        const [lx, ly] = pts[pts.length - 1];
        svg += text(lx - 8, ly + 4, 'ideal', { size: 10.5, fill: COLORS.muted, anchor: 'end' });
    }

    // the series
    for (const s of series) {
        let d = '';
        s.values.forEach((v, i) => {
            if (v == null) return;
            d += `${d === '' ? 'M' : 'L'}${xAt(i).toFixed(1)},${yAt(v).toFixed(1)} `;
        });
        svg += `<path d="${d}" fill="none" stroke="${s.color}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>`;
        s.values.forEach((v, i) => {
            if (v == null) return;
            svg += `<circle cx="${xAt(i).toFixed(1)}" cy="${yAt(v).toFixed(1)}" r="4" fill="${s.color}" stroke="${COLORS.surface}" stroke-width="2"/>`;
        });
    }

    // direct label at the end of each line, e.g. "C# 35.1 ms" (text in ink; the dot carries the colour)
    if (labelEnds) {
        const ends = series.map((s) => {
            const i = s.values.findLastIndex((v) => v != null);
            return { s, i, v: s.values[i], y: yAt(s.values[i]) };
        }).filter((e) => e.i === xs.length - 1).sort((a, b) => a.y - b.y);
        for (let k = 1; k < ends.length; k++)            // keep end labels at least 14px apart
            if (ends[k].y - ends[k - 1].y < 14) ends[k].y = ends[k - 1].y + 14;
        for (const e of ends)
            svg += text(xAt(e.i) + 10, e.y + 4, `${e.s.name} ${fmt(e.v)}`, { size: 11, fill: COLORS.ink2 });
    }

    return svg + '</svg>';
}

/**
 * Horizontal bars, one row per item, optionally grouped (several bars per row).
 * rows: [{ label, bars: [{ value, color, text }] }]
 */
export function barChart({ title, subtitle, rows, unit, legendItems }) {
    const barH = 14, gap = 2, rowGap = 14;
    const left = 190, right = 130;
    const head = header(title, subtitle, legendItems, 'square');
    const top = head.top;
    const rowH = (r) => r.bars.length * barH + (r.bars.length - 1) * gap;
    const plotH = rows.reduce((h, r) => h + rowH(r) + rowGap, 0);
    const H = top + plotH + 36;
    const pw = W - left - right;
    const max = Math.max(...rows.flatMap((r) => r.bars.map((b) => b.value)));
    const { max: axisMax, step } = niceScale(max * 1.02);
    const xAt = (v) => left + (v / axisMax) * pw;

    let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">`;
    svg += `<rect width="${W}" height="${H}" fill="${COLORS.surface}"/>` + head.svg;

    for (let v = 0; v <= axisMax + 1e-9; v += step) {
        svg += `<line x1="${xAt(v)}" y1="${top - 6}" x2="${xAt(v)}" y2="${top + plotH - rowGap + 4}" stroke="${v === 0 ? COLORS.axis : COLORS.grid}" stroke-width="1"/>`;
        svg += text(xAt(v), top + plotH + 8, formatNumber(v), { size: 11, fill: COLORS.muted, anchor: 'middle' });
    }
    svg += text(left + pw / 2, top + plotH + 26, unit, { size: 11, fill: COLORS.muted, anchor: 'middle' });

    let y = top;
    for (const r of rows) {
        svg += text(left - 10, y + rowH(r) / 2 + 4, r.label, { size: 11.5, fill: COLORS.ink2, anchor: 'end' });
        for (const b of r.bars) {
            const w = Math.max(xAt(b.value) - left, 1.5);
            const rr = Math.min(4, w / 2);   // 4px rounded end on the data side, square at the baseline
            svg += `<path d="M${left},${y} h${w - rr} a${rr},${rr} 0 0 1 ${rr},${rr} v${barH - 2 * rr} a${rr},${rr} 0 0 1 -${rr},${rr} h-${w - rr} z" fill="${b.color}"/>`;
            svg += text(left + w + 6, y + barH - 3, b.text ?? formatNumber(b.value), { size: 11, fill: COLORS.ink2 });
            y += barH + gap;
        }
        y += rowGap - gap;
    }
    return svg + '</svg>';
}

/**
 * Stacked horizontal bars (Task 2: I/O time + CPU time per run).
 * rows: [{ label, parts: [{ value, color }], text }]
 */
export function stackedBarChart({ title, subtitle, rows, unit, legendItems }) {
    const barH = 18, rowGap = 14, left = 190, right = 130;
    const head = header(title, subtitle, legendItems, 'square');
    const top = head.top;
    const plotH = rows.length * (barH + rowGap);
    const H = top + plotH + 36;
    const pw = W - left - right;
    const max = Math.max(...rows.map((r) => r.parts.reduce((a, p) => a + p.value, 0)));
    const { max: axisMax, step } = niceScale(max * 1.02);
    const xAt = (v) => left + (v / axisMax) * pw;

    let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">`;
    svg += `<rect width="${W}" height="${H}" fill="${COLORS.surface}"/>` + head.svg;

    for (let v = 0; v <= axisMax + 1e-9; v += step) {
        svg += `<line x1="${xAt(v)}" y1="${top - 6}" x2="${xAt(v)}" y2="${top + plotH - rowGap + 4}" stroke="${v === 0 ? COLORS.axis : COLORS.grid}" stroke-width="1"/>`;
        svg += text(xAt(v), top + plotH + 8, formatNumber(v), { size: 11, fill: COLORS.muted, anchor: 'middle' });
    }
    svg += text(left + pw / 2, top + plotH + 26, unit, { size: 11, fill: COLORS.muted, anchor: 'middle' });

    let y = top;
    for (const r of rows) {
        svg += text(left - 10, y + barH / 2 + 4, r.label, { size: 11.5, fill: COLORS.ink2, anchor: 'end' });
        let x = left;
        r.parts.forEach((p, k) => {
            const w = Math.max(xAt(p.value) - left, 0);
            const last = k === r.parts.length - 1;
            const drawW = Math.max(w - (last ? 0 : 2), 0.8);   // 2px surface gap between segments
            if (last && drawW > 4) {
                svg += `<path d="M${x},${y} h${drawW - 4} a4,4 0 0 1 4,4 v${barH - 8} a4,4 0 0 1 -4,4 h-${drawW - 4} z" fill="${p.color}"/>`;
            } else {
                svg += `<rect x="${x}" y="${y}" width="${drawW}" height="${barH}" fill="${p.color}"/>`;
            }
            x += w;
        });
        svg += text(x + 6, y + barH - 4, r.text, { size: 11, fill: COLORS.ink2 });
        y += barH + rowGap;
    }
    return svg + '</svg>';
}

/** SVG text -> PNG bytes (3x scale so the image stays sharp in print). */
export function toPng(svg) {
    const resvg = new Resvg(svg, {
        fitTo: { mode: 'zoom', value: 3 },
        font: { loadSystemFonts: true, defaultFontFamily: 'Segoe UI' },
        background: COLORS.surface,
    });
    return resvg.render().asPng();
}
