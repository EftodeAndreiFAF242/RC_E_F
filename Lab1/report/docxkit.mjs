// =============================================================================
//  Small helpers on top of the "docx" library, so the report text can be written
//  as plain strings:  **bold**, *italic* and `code` inside a paragraph are
//  turned into the matching Word formatting.
// =============================================================================
import {
    AlignmentType, BorderStyle, ImageRun, LevelFormat, Paragraph, ShadingType, Table, TableCell,
    TableRow, TextRun, WidthType, HeadingLevel, ExternalHyperlink,
} from 'docx';

export const FONT = 'Times New Roman';
export const CODE_FONT = 'Consolas';
export const CONTENT_WIDTH = 9355;   // A4 (11906) minus 3 cm left and 1.5 cm right margins, in DXA (1/1440 inch)

/** "**bold** text with `code`" -> TextRuns */
export function runs(text, base = {}) {
    const out = [];
    const re = /(\*\*[^*]+\*\*|`[^`]+`|\*[^*]+\*)/g;
    let last = 0, m;
    while ((m = re.exec(text)) !== null) {
        if (m.index > last) out.push(new TextRun({ ...base, text: text.slice(last, m.index) }));
        const token = m[0];
        // `...base` comes FIRST, so the formatting of the token (bold, code font) wins over the base
        if (token.startsWith('**')) out.push(new TextRun({ ...base, text: token.slice(2, -2), bold: true }));
        else if (token.startsWith('`')) out.push(new TextRun({ ...base, text: token.slice(1, -1), font: CODE_FONT, size: (base.size ?? 24) - 3 }));
        else out.push(new TextRun({ ...base, text: token.slice(1, -1), italics: true }));
        last = m.index + token.length;
    }
    if (last < text.length) out.push(new TextRun({ ...base, text: text.slice(last) }));
    return out;
}

export const P = (text, opts = {}) => new Paragraph({
    alignment: opts.align ?? AlignmentType.JUSTIFIED,
    spacing: { after: 120, line: 288 },
    indent: opts.indent ? { firstLine: 567 } : undefined,
    keepNext: opts.keepNext,
    children: runs(text, opts.run ?? {}),
});

export const H1 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_1, pageBreakBefore: false, children: [new TextRun(text)] });
export const H2 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(text)] });
export const H3 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(text)] });

export const bullet = (text) => new Paragraph({
    numbering: { reference: 'bullets', level: 0 },
    spacing: { after: 60, line: 276 },
    alignment: AlignmentType.LEFT,
    children: runs(text),
});

let listInstance = 0;
/** A numbered list that starts again at 1. */
export function numbered(items) {
    listInstance++;
    return items.map((text) => new Paragraph({
        numbering: { reference: 'numbers', level: 0, instance: listInstance },
        spacing: { after: 80, line: 276 },
        alignment: AlignmentType.JUSTIFIED,
        children: runs(text),
    }));
}

/** A question in bold followed by its answer paragraphs. */
export function qa(question, answers) {
    return [
        new Paragraph({ spacing: { before: 160, after: 80 }, keepNext: true, children: runs(`**${question}**`) }),
        ...answers.map((a) => P(a)),
    ];
}

/** A grey box with monospaced code lines. */
export function code(lines) {
    return lines.split('\n').map((line, i, all) => new Paragraph({
        shading: { type: ShadingType.CLEAR, color: 'auto', fill: 'F3F2EF' },
        spacing: { after: i === all.length - 1 ? 160 : 0, line: 240 },
        keepNext: i < all.length - 1,
        children: [new TextRun({ text: line === '' ? ' ' : line, font: CODE_FONT, size: 17 })],
    }));
}

const border = { style: BorderStyle.SINGLE, size: 4, color: 'C3C2B7' };
const borders = { top: border, bottom: border, left: border, right: border };

/**
 * A table. widths are fractions of the content width (they are scaled to DXA).
 * align: per column 'left' | 'right' | 'center'.
 */
export function table(headers, rows, { widths, align = [] } = {}) {
    const n = headers.length;
    const fr = widths ?? Array(n).fill(1 / n);
    const sum = fr.reduce((a, b) => a + b, 0);
    const dxa = fr.map((f) => Math.floor((f / sum) * CONTENT_WIDTH));
    dxa[n - 1] += CONTENT_WIDTH - dxa.reduce((a, b) => a + b, 0);   // make them add up exactly
    const al = (i) => ({ right: AlignmentType.RIGHT, center: AlignmentType.CENTER }[align[i]] ?? AlignmentType.LEFT);

    const cell = (text, i, header) => new TableCell({
        width: { size: dxa[i], type: WidthType.DXA },
        borders,
        shading: header ? { type: ShadingType.CLEAR, color: 'auto', fill: 'EDECE8' } : undefined,
        margins: { top: 40, bottom: 40, left: 90, right: 90 },
        children: [new Paragraph({ alignment: header ? AlignmentType.CENTER : al(i), children: runs(String(text), header ? { size: 19, bold: true } : { size: 19 }) })],
    });

    return new Table({
        width: { size: CONTENT_WIDTH, type: WidthType.DXA },
        columnWidths: dxa,
        rows: [
            new TableRow({ tableHeader: true, children: headers.map((h, i) => cell(h, i, true)) }),
            ...rows.map((r) => new TableRow({ cantSplit: true, children: r.map((c, i) => cell(c, i, false)) })),
        ],
    });
}

export const caption = (text) => new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { before: 60, after: 200 },
    children: runs(text, { size: 20, italics: true }),
});

/** A chart image (PNG bytes) at full content width, followed by its caption. */
export function figure(png, widthPx, heightPx, captionText) {
    const width = 620;   // pixels at 96 dpi = 6.46 inch, the content width
    return [
        new Paragraph({
            alignment: AlignmentType.CENTER,
            keepNext: true,
            spacing: { before: 120, after: 0 },
            children: [new ImageRun({ type: 'png', data: png, transformation: { width, height: Math.round((width * heightPx) / widthPx) } })],
        }),
        caption(captionText),
    ];
}

export const link = (text, url) => new ExternalHyperlink({ link: url, children: [new TextRun({ text, style: 'Hyperlink' })] });

export const spacer = (pts = 12) => new Paragraph({ spacing: { after: pts * 20 }, children: [] });

export const numberingConfig = {
    config: [
        {
            reference: 'bullets',
            levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT,
                       style: { paragraph: { indent: { left: 567, hanging: 283 } } } }],
        },
        {
            reference: 'numbers',
            levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT,
                       style: { paragraph: { indent: { left: 567, hanging: 340 } } } }],
        },
    ],
};
