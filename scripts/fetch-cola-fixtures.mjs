import sharp from "sharp";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const colasDir = path.join(root, "testdata", "colas");

const forceReal = process.argv.includes("--force-real");

const ttbWarning =
  "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

/** @type {Array<{
 *   ttbId: string;
 *   fancifulName: string;
 *   brandName: string;
 *   origin: string;
 *   originType: "domestic" | "import";
 *   classType: string;
 *   productCategory: string;
 *   abvPercent: number;
 *   netContents: string;
 *   bottlerProducerAddress: string;
 *   countryOfOrigin?: string;
 *   wikimediaSearches?: string[];
 * }>} */
const colas = [
  {
    ttbId: "03211001000018",
    fancifulName: "CASCADE VAL",
    brandName: "CASCADE WINERY",
    origin: "Michigan, USA",
    originType: "domestic",
    classType: "TABLE RED WINE",
    productCategory: "wine",
    abvPercent: 13.5,
    netContents: "750 mL",
    bottlerProducerAddress: "Produced and Bottled by Cascade Winery, Traverse City, MI 49684",
    wikimediaSearches: [
      "Michigan red wine bottle",
      "wine bottle label front",
      "750ml wine bottle photo",
    ],
  },
  {
    ttbId: "11115001000373",
    fancifulName: "HONEY & BOURBON",
    brandName: "BARENJAGER",
    origin: "Germany",
    originType: "import",
    classType: "OTHER SPECIALTIES & PROPRIETARIES",
    productCategory: "distilled_spirits",
    abvPercent: 35.0,
    netContents: "750 mL",
    bottlerProducerAddress: "Imported by Barenjager USA, Deerfield, IL 60015",
    countryOfOrigin: "Germany",
    wikimediaSearches: [
      "Barenjager honey liqueur bottle",
      "Barenjager bottle",
      "honey liqueur bottle label",
    ],
  },
  {
    ttbId: "11364001000181",
    fancifulName: "DEBUTANTE",
    brandName: "STILLWATER ARTISANAL",
    origin: "Maryland, USA",
    originType: "domestic",
    classType: "MALT BEVERAGES SPECIALITIES - FLAVORED",
    productCategory: "beer",
    abvPercent: 6.5,
    netContents: "16 fl oz",
    bottlerProducerAddress: "Brewed and Packaged by Stillwater Artisanal, Baltimore, MD 21224",
    wikimediaSearches: [
      "Stillwater Artisanal beer can",
      "craft beer can label photo",
      "Stillwater beer bottle",
    ],
  },
  {
    ttbId: "12207001000536",
    fancifulName: "ACHOLADO",
    brandName: "VIJO TONEL",
    origin: "Peru",
    originType: "import",
    classType: "OTHER GRAPE BRANDY (PISCO, GRAPPA)",
    productCategory: "distilled_spirits",
    abvPercent: 42.0,
    netContents: "750 mL",
    bottlerProducerAddress: "Imported by Vijo Tonel Imports, Miami, FL 33101",
    countryOfOrigin: "Peru",
    wikimediaSearches: [
      "Pisco bottle Peru",
      "pisco liquor bottle label",
      "Peruvian pisco bottle photo",
    ],
  },
  {
    ttbId: "15107001000276",
    fancifulName: "STONE'S THROW",
    brandName: "STONE'S THROW",
    origin: "Minnesota, USA",
    originType: "domestic",
    classType: "STRAIGHT BOURBON WHISKY",
    productCategory: "distilled_spirits",
    abvPercent: 45.0,
    netContents: "750 mL",
    bottlerProducerAddress:
      "Distilled and Bottled by RockFilter Distillery, Spring Grove, MN 55974",
    wikimediaSearches: [
      "bourbon whiskey bottle label",
      "RockFilter bourbon bottle",
      "straight bourbon bottle photo",
    ],
  },
];

const PUBLIC_COLA_SEARCH_URL =
  "https://ttbonline.gov/colasonline/publicSearchColasBasic.do";
const PUBLIC_COLA_PRINT_HINT =
  "https://ttbonline.gov/colasonline/publicSearchColasBasic.do — search TTB ID, open certificate, use browser Print → Save as PDF/PNG";

const escapeXml = (value) =>
  value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

const wrapText = (text, lineLength = 55) => {
  const words = text.split(" ");
  const lines = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (next.length > lineLength) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines;
};

const wrapWarning = (text, lineLength = 70) => {
  const words = text.split(" ");
  const lines = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (next.length > lineLength) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines;
};

const buildSyntheticSvg = (cola) => {
  const warningLines = wrapWarning(ttbWarning, 55);
  const warningSvg = warningLines
    .map((line, index) => {
      const weight = index === 0 ? ' font-weight="700"' : "";
      return `<text x="50" y="${920 + index * 28}" font-family="Arial, Helvetica, sans-serif" font-size="20"${weight} fill="#111">${escapeXml(line)}</text>`;
    })
    .join("\n");

  const addressLines = wrapText(cola.bottlerProducerAddress, 50);
  const addressSvg = addressLines
    .map(
      (line, index) =>
        `<text x="450" y="${560 + index * 26}" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="18" fill="#333">${escapeXml(line)}</text>`,
    )
    .join("\n");

  const countrySvg = cola.countryOfOrigin
    ? `<text x="450" y="${560 + addressLines.length * 26 + 20}" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="18" fill="#333">Product of ${escapeXml(cola.countryOfOrigin)}</text>`
    : "";

  return `<svg width="900" height="1200" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" fill="#f5f0e8"/>
  <rect x="20" y="20" width="860" height="48" fill="#fff3cd" stroke="#856404" stroke-width="2"/>
  <text x="450" y="52" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="16" font-weight="700" fill="#856404">SYNTHETIC FIXTURE — TTB ID ${escapeXml(cola.ttbId)}</text>
  <text x="450" y="110" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="22" fill="#555">${escapeXml(cola.brandName)}</text>
  <text x="450" y="175" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="48" font-weight="700" fill="#1a4480">${escapeXml(cola.fancifulName)}</text>
  <text x="450" y="235" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="26" fill="#333">${escapeXml(cola.classType)}</text>
  <text x="450" y="295" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="34" fill="#333">${cola.abvPercent.toFixed(1)}% ALC. BY VOL.</text>
  <text x="450" y="350" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="26" fill="#333">${escapeXml(cola.netContents)}</text>
  <text x="450" y="400" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="18" fill="#666">COLA ${escapeXml(cola.ttbId)} · ${escapeXml(cola.origin)}</text>
  ${addressSvg}
  ${countrySvg}
  ${warningSvg}
</svg>`;
};

const buildMeta = (cola, imageSource, imageSourceNotes) => ({
  ttbId: cola.ttbId,
  fancifulName: cola.fancifulName,
  brandName: cola.brandName,
  origin: cola.origin,
  originType: cola.originType,
  classType: cola.classType,
  colaRegistryUrl: PUBLIC_COLA_SEARCH_URL,
  colaRegistrySearchHint: `Search TTB ID ${cola.ttbId} at Public COLA Registry`,
  representativeFieldsNote:
    "abvPercent and netContents in expectedLabelFields are representative placeholders inferred from product class for OCR fixture testing. They are not verified against the COLA certificate image.",
  imageSource,
  imageSourceNotes,
  expectedLabelFields: {
    brandName: cola.brandName,
    classTypeDesignation: cola.classType,
    abvPercent: cola.abvPercent,
    netContents: cola.netContents,
    bottlerProducerAddress: cola.bottlerProducerAddress,
    productCategory: cola.productCategory,
    ttbWarningText: ttbWarning,
    boldWarningPhrase: "GOVERNMENT WARNING:",
    ...(cola.countryOfOrigin ? { countryOfOrigin: cola.countryOfOrigin } : {}),
  },
});

const tryFetchPublicColaMetadata = async (ttbId) => {
  const searchUrl = `${PUBLIC_COLA_SEARCH_URL}?ttbId=${encodeURIComponent(ttbId)}`;
  try {
    const response = await fetch(searchUrl, {
      method: "GET",
      headers: { Accept: "text/html" },
      signal: AbortSignal.timeout(8000),
    });
    if (!response.ok) {
      return { ok: false, reason: `HTTP ${response.status}` };
    }
    const html = await response.text();
    if (/sign\s*in|login|oauth/i.test(html) && !html.includes(ttbId)) {
      return { ok: false, reason: "Registry returned login page; manual printable download required" };
    }
    if (html.includes(ttbId)) {
      return { ok: true, reason: "Registry HTML mentions TTB ID (image still requires manual printable save)" };
    }
    return { ok: false, reason: "TTB ID not found in registry response" };
  } catch (error) {
    return { ok: false, reason: error instanceof Error ? error.message : "fetch failed" };
  }
};

const isAllowedLicense = (license, licenseUrl) => {
  const combined = `${license} ${licenseUrl}`.toLowerCase();
  return (
    /public domain|cc0|pd-us|cc-by|cc by|cc-by-sa|cc by-sa|creative commons zero/i.test(combined) ||
    /creativecommons.org\/publicdomain|creativecommons.org\/licenses\/by/i.test(combined)
  );
};

const scoreWikimediaCandidate = (title, width, height) => {
  const lower = title.toLowerCase();
  let score = 0;
  if (/bottle|label|liquor|spirit|wine|beer|whisk|bourbon|pisco|can|liqueur/.test(lower)) {
    score += 3;
  }
  if (/photo|jpg|jpeg|png/.test(lower)) {
    score += 1;
  }
  if (/logo|icon|svg|diagram|map|chart|advertisement|poster/.test(lower)) {
    score -= 4;
  }
  const pixels = (width ?? 0) * (height ?? 0);
  if (pixels >= 400_000) {
    score += 2;
  } else if (pixels >= 150_000) {
    score += 1;
  }
  return score;
};

const searchWikimedia = async (searchTerm) => {
  const params = new URLSearchParams({
    action: "query",
    format: "json",
    generator: "search",
    gsrsearch: `filetype:bitmap ${searchTerm}`,
    gsrlimit: "8",
    prop: "imageinfo",
    iiprop: "url|extmetadata|size",
    iiurlwidth: "1400",
  });

  const response = await fetch(`https://commons.wikimedia.org/w/api.php?${params}`, {
    signal: AbortSignal.timeout(12000),
    headers: { "User-Agent": "treasury-label-verification-plan/0.2 (COLA fixture fetch)" },
  });
  if (!response.ok) {
    return [];
  }
  const data = await response.json();
  const pages = data?.query?.pages;
  if (!pages) {
    return [];
  }

  /** @type {Array<{ title: string; thumburl: string; license: string; licenseUrl: string; score: number }>} */
  const candidates = [];

  for (const page of Object.values(pages)) {
    const info = page?.imageinfo?.[0];
    if (!info?.thumburl) {
      continue;
    }
    const license = info?.extmetadata?.LicenseShortName?.value ?? "";
    const licenseUrl = info?.extmetadata?.LicenseUrl?.value ?? "";
    if (!isAllowedLicense(license, licenseUrl)) {
      continue;
    }
    candidates.push({
      title: page.title ?? searchTerm,
      thumburl: info.thumburl,
      license,
      licenseUrl,
      score: scoreWikimediaCandidate(page.title ?? "", info.width, info.height),
    });
  }

  return candidates.sort((a, b) => b.score - a.score);
};

const tryWikimediaCommonsImage = async (cola) => {
  const searches = cola.wikimediaSearches ?? [];
  /** @type {Array<{ title: string; thumburl: string; license: string; licenseUrl: string; score: number }>} */
  const allCandidates = [];

  for (const term of searches) {
    try {
      const found = await searchWikimedia(term);
      allCandidates.push(...found);
    } catch {
      // try next search term
    }
  }

  const best = allCandidates.sort((a, b) => b.score - a.score)[0];
  if (!best) {
    return null;
  }

  try {
    const imageResponse = await fetch(best.thumburl, {
      signal: AbortSignal.timeout(20000),
      headers: { "User-Agent": "treasury-label-verification-plan/0.2 (COLA fixture fetch)" },
    });
    if (!imageResponse.ok) {
      return null;
    }
    const buffer = Buffer.from(await imageResponse.arrayBuffer());
    const metadata = await sharp(buffer).metadata();
    if ((metadata.width ?? 0) < 200 || (metadata.height ?? 0) < 200) {
      return null;
    }
    return {
      buffer,
      source: "wikimedia_commons",
      notes: `Wikimedia Commons: ${best.title}; License: ${best.license}; ${best.licenseUrl}. Photo used as OCR fixture paired with COLA ${cola.ttbId} metadata — label text on bottle may differ from certificate fields.`,
    };
  } catch {
    return null;
  }
};

const writeSyntheticPng = async (cola, pngPath) => {
  const svg = buildSyntheticSvg(cola);
  await sharp(Buffer.from(svg)).png().toFile(pngPath);
};

await fs.mkdir(colasDir, { recursive: true });

console.log("COLA fixture fetch — preferred: real bottle photos (Wikimedia Commons) paired with COLA metadata");
console.log(`Registry: ${PUBLIC_COLA_SEARCH_URL}`);
if (forceReal) {
  console.log("Mode: --force-real (replace existing PNGs when Commons match found)");
}
console.log("");

for (const cola of colas) {
  const metaPath = path.join(colasDir, `${cola.ttbId}.meta.json`);
  const pngPath = path.join(colasDir, `${cola.ttbId}.png`);
  const pngExists = await fs
    .access(pngPath)
    .then(() => true)
    .catch(() => false);

  const registryAttempt = await tryFetchPublicColaMetadata(cola.ttbId);
  console.log(`[${cola.ttbId}] Registry probe: ${registryAttempt.ok ? "partial" : "skipped"} — ${registryAttempt.reason}`);

  let imageSource = "synthetic";
  let imageSourceNotes =
    "Generated by scripts/fetch-cola-fixtures.mjs using public COLA metadata text. Replace with printable registry image when available.";

  const shouldFetchImage = !pngExists || forceReal;
  if (shouldFetchImage) {
    const wikimedia = await tryWikimediaCommonsImage(cola);
    if (wikimedia) {
      await sharp(wikimedia.buffer).rotate().png().toFile(pngPath);
      imageSource = wikimedia.source;
      imageSourceNotes = wikimedia.notes;
      console.log(`[${cola.ttbId}] Image: Wikimedia Commons (real bottle photo)`);
    } else if (!pngExists) {
      await writeSyntheticPng(cola, pngPath);
      console.log(`[${cola.ttbId}] Image: synthetic PNG (no Wikimedia match)`);
    } else {
      imageSource = "manual_or_existing";
      imageSourceNotes =
        "Existing PNG preserved (--force-real could not find Wikimedia replacement).";
      console.log(`[${cola.ttbId}] Image: kept existing (no Wikimedia match with --force-real)`);
    }
  } else {
    imageSource = "manual_or_existing";
    imageSourceNotes =
      "Existing PNG preserved. Run with --force-real to replace with Wikimedia bottle photos.";
    console.log(`[${cola.ttbId}] Image: kept existing ${cola.ttbId}.png`);
  }

  const meta = buildMeta(cola, imageSource, imageSourceNotes);
  await fs.writeFile(metaPath, `${JSON.stringify(meta, null, 2)}\n`);
  console.log(`[${cola.ttbId}] Wrote ${cola.ttbId}.meta.json`);
}

console.log("");
console.log(`Done. COLA fixtures in ${colasDir}`);
