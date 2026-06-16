import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const colasDir = path.join(root, "testdata", "colas");

const userAgent = "LabelVerificationDemo/0.2 (treasury-label-verification-plan; COLA fixture fetch)";

/** Curated real bottle/can photos — direct upload.wikimedia.org URLs avoid API rate limits. */
const curatedPhotos = [
  {
    ttbId: "11115001000373",
    fileName: "Bärenjäger_bottle.jpg",
    title: "File:Bärenjäger bottle.jpg",
    license: "Public domain",
    thumbWidth: 800,
  },
  {
    ttbId: "15107001000276",
    fileName: "Wild_Turkey_Rare_Breed.jpg",
    title: "File:Wild Turkey Rare Breed.jpg",
    license: "CC0 1.0",
    thumbWidth: 1200,
  },
  {
    ttbId: "11364001000181",
    fileName: "RPS_The_Spock_Sour.jpg",
    title: "File:RPS The Spock Sour.jpg",
    license: "CC BY-SA 4.0",
    thumbWidth: 1200,
  },
  {
    ttbId: "12207001000536",
    fileName: "Pisco_Chileno.jpg",
    title: "File:Pisco Chileno.jpg",
    license: "GFDL / CC-BY-SA (verify on Commons)",
    thumbWidth: 800,
  },
  {
    ttbId: "03211001000018",
    fileName: "A_Chapter_of_wine_(Unsplash).jpg",
    title: "File:A Chapter of wine (Unsplash).jpg",
    license: "CC0 1.0",
    thumbWidth: 1200,
  },
];

const wikimediaUploadUrl = (fileName, thumbWidth = null) => {
  const hash = crypto.createHash("md5").update(fileName).digest("hex");
  const base = `https://upload.wikimedia.org/wikipedia/commons/${hash[0]}/${hash.slice(0, 2)}/${encodeURIComponent(fileName)}`;
  if (!thumbWidth) {
    return base;
  }
  return `https://upload.wikimedia.org/wikipedia/commons/thumb/${hash[0]}/${hash.slice(0, 2)}/${encodeURIComponent(fileName)}/${thumbWidth}px-${encodeURIComponent(fileName)}`;
};

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const fetchWithRetry = async (url, attempts = 5) => {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const response = await fetch(url, {
      headers: { "User-Agent": userAgent },
      signal: AbortSignal.timeout(45000),
    });
    if (response.status === 429 && attempt < attempts) {
      const waitMs = attempt * 8000;
      console.warn(`  Rate limited — waiting ${waitMs / 1000}s (attempt ${attempt}/${attempts})`);
      await sleep(waitMs);
      continue;
    }
    return response;
  }
  throw new Error("Exceeded retry attempts");
};

for (const item of curatedPhotos) {
  const url = wikimediaUploadUrl(item.fileName, item.thumbWidth ?? 1200);
  await sleep(4000);
  console.log(`[${item.ttbId}] Fetching ${item.title}`);

  try {
    const response = await fetchWithRetry(url);
    if (!response.ok) {
      console.warn(`[${item.ttbId}] HTTP ${response.status} — skipped`);
      continue;
    }

    const buffer = Buffer.from(await response.arrayBuffer());
    const metadata = await sharp(buffer).metadata();
    if ((metadata.width ?? 0) < 180 || (metadata.height ?? 0) < 180) {
      console.warn(`[${item.ttbId}] Image too small — skipped`);
      continue;
    }

    const pngPath = path.join(colasDir, `${item.ttbId}.png`);
    await sharp(buffer)
      .rotate()
      .resize({ width: 1600, height: 2400, fit: "inside", withoutEnlargement: true })
      .png({ compressionLevel: 8 })
      .toFile(pngPath);

    const metaPath = path.join(colasDir, `${item.ttbId}.meta.json`);
    const existing = JSON.parse(await fs.readFile(metaPath, "utf8"));
    existing.imageSource = "wikimedia_commons";
    existing.imageSourceNotes = `Wikimedia Commons: ${item.title}; License: ${item.license}. Real bottle/can photo used as OCR fixture paired with COLA ${item.ttbId} metadata — text visible on product may differ from certificate fields.`;
    await fs.writeFile(metaPath, `${JSON.stringify(existing, null, 2)}\n`);

    console.log(
      `[${item.ttbId}] Saved ${metadata.width}x${metadata.height} (${Math.round(buffer.length / 1024)} KB)`,
    );
  } catch (error) {
    console.warn(
      `[${item.ttbId}] Failed:`,
      error instanceof Error ? error.message : error,
    );
  }
}

console.log("Curated photo download complete.");
