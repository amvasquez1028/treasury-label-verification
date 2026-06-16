import sharp from "sharp";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const colasDir = path.join(root, "testdata", "colas");

const ttbWarning =
  "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

/** @type {Record<string, { bottleTint?: string; labelBg?: string; textColor?: string; accentColor?: string }>} */
const bottleStyles = {
  "11115001000373": { bottleTint: "#3d2914", labelBg: "#fffef5" },
  "11364001000181": { bottleTint: "#1a1a2e", labelBg: "#ffffff" },
  "03211001000018": { bottleTint: "#2d0a0a", labelBg: "#fffef8" },
  "15107001000276": { bottleTint: "#8B4513", labelBg: "#fff8ef" },
  "12207001000536": { bottleTint: "#c8b898", labelBg: "#ffffff" },
  "20085001000218": {
    bottleTint: "#0a1a4a",
    labelBg: "#0c1f52",
    textColor: "#ffffff",
    accentColor: "#d4af37",
  },
};

const escapeXml = (value) =>
  value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

const wrapText = (text, lineLength = 44) => {
  const words = text.split(" ");
  const lines = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (next.length > lineLength) {
      if (current) lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines;
};

const buildLabelSvg = (cola, style) => {
  const labelBg = style.labelBg ?? "#ffffff";
  const textColor = style.textColor ?? "#111111";
  const accentColor = style.accentColor ?? "#1a4480";
  const warningLines = wrapText(ttbWarning, 42);
  const warningSvg = warningLines
    .map(
      (line) =>
        `<text x="36" y="${780 + warningLines.indexOf(line) * 28}" font-family="Arial, Helvetica, sans-serif" font-size="22" font-weight="700" fill="${textColor === "#ffffff" ? "#ffffff" : "#000000"}">${escapeXml(line)}</text>`,
    )
    .join("\n");

  const addressLines = wrapText(cola.bottlerProducerAddress, 38);
  const addressSvg = addressLines
    .map(
      (line, index) =>
        `<text x="360" y="${560 + index * 30}" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="22" fill="${textColor}">${escapeXml(line)}</text>`,
    )
    .join("\n");

  const countrySvg = cola.countryOfOrigin
    ? `<text x="360" y="${560 + addressLines.length * 30 + 24}" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="22" fill="${textColor}">Product of ${escapeXml(cola.countryOfOrigin)}</text>`
    : "";

  return `<svg width="720" height="1100" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" fill="${labelBg}"/>
  <text x="360" y="80" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" font-weight="700" fill="${textColor}">${escapeXml(cola.brandName)}</text>
  <text x="360" y="170" text-anchor="middle" font-family="Georgia, serif" font-size="52" font-weight="700" fill="${accentColor}">${escapeXml(cola.fancifulName)}</text>
  <text x="360" y="240" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" fill="${textColor}">${escapeXml(cola.classType)}</text>
  <text x="360" y="310" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="36" font-weight="700" fill="${textColor}">${cola.abvPercent.toFixed(1)}% ALC. BY VOL.</text>
  <text x="360" y="370" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="32" font-weight="700" fill="${textColor}">${escapeXml(cola.netContents)}</text>
  ${addressSvg}
  ${countrySvg}
  ${warningSvg}
</svg>`;
};

const loadColaMeta = async (ttbId) => {
  const metaPath = path.join(colasDir, `${ttbId}.meta.json`);
  const raw = await fs.readFile(metaPath, "utf8");
  return JSON.parse(raw);
};

const renderSyntheticBottle = async (ttbId, colaFromMeta) => {
  const cola = {
    ttbId,
    brandName: colaFromMeta.brandName ?? colaFromMeta.expectedLabelFields?.brandName,
    fancifulName:
      colaFromMeta.fancifulName ?? colaFromMeta.expectedLabelFields?.fancifulName ?? colaFromMeta.brandName,
    classType: colaFromMeta.classType ?? colaFromMeta.expectedLabelFields?.classTypeDesignation,
    abvPercent: colaFromMeta.expectedLabelFields?.abvPercent ?? 40,
    netContents: colaFromMeta.expectedLabelFields?.netContents ?? "750 mL",
    bottlerProducerAddress: colaFromMeta.expectedLabelFields?.bottlerProducerAddress ?? "",
    countryOfOrigin: colaFromMeta.expectedLabelFields?.countryOfOrigin,
  };

  const style = bottleStyles[ttbId] ?? { labelBg: "#ffffff", bottleTint: "#2a1810" };
  const labelSvg = buildLabelSvg(cola, style);
  const labelPng = await sharp(Buffer.from(labelSvg)).png().toBuffer();

  const width = 1200;
  const height = 1800;
  const tint = style.bottleTint ?? "#2a1810";
  const bottleSvg = `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" style="stop-color:#3a3a48"/>
      <stop offset="100%" style="stop-color:#101018"/>
    </linearGradient>
    <linearGradient id="glass" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" style="stop-color:${tint};stop-opacity:0.98"/>
      <stop offset="35%" style="stop-color:#ffffff;stop-opacity:0.14"/>
      <stop offset="100%" style="stop-color:${tint};stop-opacity:0.96"/>
    </linearGradient>
  </defs>
  <rect width="100%" height="100%" fill="url(#bg)"/>
  <ellipse cx="600" cy="1680" rx="280" ry="34" fill="#000000" opacity="0.45"/>
  <rect x="455" y="140" width="290" height="90" rx="12" fill="url(#glass)"/>
  <rect x="390" y="220" width="420" height="1180" rx="30" fill="url(#glass)" stroke="#ffffff" stroke-opacity="0.1"/>
  <rect x="408" y="340" width="384" height="960" rx="4" fill="#ffffff" opacity="0.03"/>
</svg>`;

  const bottleBg = await sharp(Buffer.from(bottleSvg)).png().toBuffer();
  const labelWidth = 384;
  const labelHeight = 960;
  const resizedLabel = await sharp(labelPng).resize(labelWidth, labelHeight, { fit: "fill" }).png().toBuffer();

  return sharp(bottleBg)
    .composite([{ input: resizedLabel, left: 408, top: 340 }])
    .png()
    .toBuffer();
};

const syntheticIds = process.argv.slice(2);
const targets =
  syntheticIds.length > 0
    ? syntheticIds
    : ["11115001000373", "11364001000181", "03211001000018", "15107001000276", "12207001000536", "20085001000218"];

await fs.mkdir(colasDir, { recursive: true });

for (const ttbId of targets) {
  const meta = await loadColaMeta(ttbId);
  const pngBuffer = await renderSyntheticBottle(ttbId, meta);
  const pngPath = path.join(colasDir, `${ttbId}.png`);
  await sharp(pngBuffer).png({ compressionLevel: 8 }).toFile(pngPath);

  meta.imageSource = "synthetic_bottle";
  meta.imageSourceNotes =
    ttbId === "20085001000218"
      ? "Synthetic Johnnie Walker Blue Label bottle mockup for OCR regression testing. User real photo is stored separately as 20085001000218-real-composite.png."
      : "Synthetic bottle mockup generated by scripts/generate-synthetic-bottles.mjs. High-contrast TTB label artwork is composited onto a procedural bottle scene for OCR pass testing.";

  if (meta.expectedLabelFields) {
    meta.expectedLabelFields.labelPresentation = ttbId === "20085001000218" ? "bottleFront" : "fullLabel";
    if (!meta.expectedLabelFields.fancifulName && meta.fancifulName) {
      meta.expectedLabelFields.fancifulName = meta.fancifulName;
    }
  }

  if (ttbId === "20085001000218") {
    meta.expectVerificationPass = true;
  }

  await fs.writeFile(path.join(colasDir, `${ttbId}.meta.json`), `${JSON.stringify(meta, null, 2)}\n`);
  console.log(`[${ttbId}] Wrote synthetic bottle PNG`);
}

console.log("Done.");
