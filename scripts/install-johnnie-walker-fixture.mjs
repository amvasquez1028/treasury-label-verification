import fs from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const colasDir = path.join(root, "testdata", "colas");
const publicSamplesDir = path.join(root, "public", "samples");

const ttbWarning =
  "GOVERNMENT WARNING: (1) According to the Surgeon General, women should not drink alcoholic beverages during pregnancy because of the risk of birth defects. (2) Consumption of alcoholic beverages impairs your ability to drive a car or operate machinery, and may cause health problems.";

const johnnieWalkerCola = {
  ttbId: "20085001000218",
  fancifulName: "BLUE LABEL",
  brandName: "JOHNNIE WALKER",
  origin: "Scotland",
  originType: "import",
  classType: "BLENDED SCOTCH WHISKY",
  productCategory: "distilled_spirits",
  colaRegistryUrl:
    "https://ttbonline.gov/colasonline/viewColaDetails.do?action=publicFormDisplay&ttbid=20085001000218",
  colaRegistrySearchHint: "Search TTB ID 20085001000218 at Public COLA Registry",
  representativeFieldsNote:
    "Expected fields match Johnnie Walker Blue Label COLA 20085001000218. Automated OCR fixture uses a synthetic blue-bottle mockup; the user Year of the Tiger product photo is preserved for reviewer demos.",
  imageSource: "real_bottle_photo",
  imageSourceNotes:
    "Primary OCR fixture: real-photo composite from user Year of the Tiger bottle (brand etching + regulatory sticker crops). COLA registry: 20085001000218.",
  expectVerificationPass: true,
  expectedLabelFields: {
    brandName: "JOHNNIE WALKER",
    fancifulName: "BLUE LABEL",
    classTypeDesignation: "BLENDED SCOTCH WHISKY",
    abvPercent: 46,
    netContents: "750 mL",
    bottlerProducerAddress: "JOHN WALKER & SONS LIMITED, SCOTLAND",
    countryOfOrigin: "Scotland",
    productCategory: "distilled_spirits",
    labelPresentation: "bottleFront",
    ttbWarningText: ttbWarning,
    boldWarningPhrase: "GOVERNMENT WARNING:",
  },
};

const sourceCandidates = [
  path.join(colasDir, "20085001000218-user-source.png"),
  path.join(
    root,
    "..",
    ".cursor",
    "projects",
    "c-Users-Alaina-Documents-AWS-Job-Hunt",
    "assets",
    "c__Users_Alaina_AppData_Roaming_Cursor_User_workspaceStorage_8cae447fdcba937352e943f65161fd43_images_image-5cfd85cc-e72f-455d-b87a-5efd99a14b1c.png",
  ),
];

const enhanceRegion = async (input, region, targetWidth) => {
  const meta = await sharp(input).metadata();
  const width = meta.width ?? 819;
  const height = meta.height ?? 1024;
  const left = Math.min(region.left, width - 1);
  const top = Math.min(region.top, height - 1);
  const extractWidth = Math.min(region.width, width - left);
  const extractHeight = Math.min(region.height, height - top);

  return sharp(input)
    .extract({ left, top, width: extractWidth, height: extractHeight })
    .resize({ width: targetWidth, withoutEnlargement: false })
    .normalize()
    .linear(1.15, -20)
    .sharpen({ sigma: 2.0, m1: 0.65, m2: 0.45 })
    .png()
    .toBuffer();
};

let sourcePath = null;
for (const candidate of sourceCandidates) {
  try {
    await fs.access(candidate);
    sourcePath = candidate;
    break;
  } catch {
    // try next
  }
}

if (!sourcePath) {
  console.error("Johnnie Walker source photo not found.");
  process.exit(1);
}

const sourceMeta = await sharp(sourcePath).metadata();
const width = sourceMeta.width ?? 819;
const height = sourceMeta.height ?? 1024;

const brandRegion = {
  left: Math.round(width * 0.34),
  top: Math.round(height * 0.14),
  width: Math.round(width * 0.38),
  height: Math.round(height * 0.44),
};

const stickerRegion = {
  left: Math.round(width * 0.36),
  top: Math.round(height * 0.74),
  width: Math.round(width * 0.34),
  height: Math.round(height * 0.18),
};

const targetWidth = 1100;
const brandPanel = await enhanceRegion(sourcePath, brandRegion, targetWidth);
const stickerPanel = await enhanceRegion(sourcePath, stickerRegion, targetWidth);
const brandMeta = await sharp(brandPanel).metadata();
const stickerMeta = await sharp(stickerPanel).metadata();
const canvasHeight = (brandMeta.height ?? 700) + (stickerMeta.height ?? 260) + 80;

const realCompositePath = path.join(colasDir, "20085001000218-real-composite.png");
await sharp({
  create: {
    width: targetWidth,
    height: canvasHeight,
    channels: 3,
    background: { r: 248, g: 246, b: 242 },
  },
})
  .composite([
    { input: brandPanel, top: 20, left: 0 },
    { input: stickerPanel, top: (brandMeta.height ?? 700) + 40, left: 0 },
  ])
  .extend({ top: 80, bottom: 80, left: 80, right: 80, background: { r: 252, g: 250, b: 246 } })
  .png({ compressionLevel: 8 })
  .toFile(realCompositePath);

await fs.copyFile(sourcePath, path.join(colasDir, "20085001000218-user-source.png"));
await fs.writeFile(path.join(colasDir, `${johnnieWalkerCola.ttbId}.meta.json`), `${JSON.stringify(johnnieWalkerCola, null, 2)}\n`);

await fs.mkdir(publicSamplesDir, { recursive: true });
await fs.copyFile(sourcePath, path.join(publicSamplesDir, "johnnie-walker-year-of-tiger-real-photo.png"));
await fs.copyFile(realCompositePath, path.join(publicSamplesDir, "johnnie-walker-real-composite.png"));

console.log(`Prepared JW real-photo assets from ${sourcePath}`);
console.log(`Run: node scripts/generate-synthetic-bottles.mjs 20085001000218`);
