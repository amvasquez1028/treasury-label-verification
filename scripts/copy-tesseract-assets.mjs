import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const publicDir = path.join(root, "public");
const tesseractDir = path.join(publicDir, "tesseract");
const tessdataDir = path.join(publicDir, "tessdata");

const workerSrc = path.join(root, "node_modules", "tesseract.js", "dist", "worker.min.js");

const resolveCoreDir = async () => {
  const pnpmCore = path.join(
    root,
    "node_modules",
    ".pnpm",
    "tesseract.js-core@7.0.0",
    "node_modules",
    "tesseract.js-core",
  );
  try {
    await fs.access(pnpmCore);
    return pnpmCore;
  } catch {
    const flat = path.join(root, "node_modules", "tesseract.js-core");
    await fs.access(flat);
    return flat;
  }
};

const coreFiles = [
  "tesseract-core.wasm.js",
  "tesseract-core-simd.wasm.js",
  "tesseract-core-lstm.wasm.js",
  "tesseract-core-simd-lstm.wasm.js",
  "tesseract-core-relaxedsimd.wasm.js",
  "tesseract-core-relaxedsimd-lstm.wasm.js",
];

const ENG_DATA_URL =
  "https://cdn.jsdelivr.net/npm/@tesseract.js-data/eng/4.0.0_best_int/eng.traineddata.gz";

const copyFile = async (src, dest) => {
  await fs.mkdir(path.dirname(dest), { recursive: true });
  await fs.copyFile(src, dest);
};

const main = async () => {
  await fs.mkdir(tesseractDir, { recursive: true });
  await fs.mkdir(tessdataDir, { recursive: true });

  await copyFile(workerSrc, path.join(tesseractDir, "worker.min.js"));

  const resolvedCoreDir = await resolveCoreDir();
  for (const name of coreFiles) {
    const src = path.join(resolvedCoreDir, name);
    try {
      await fs.access(src);
      await copyFile(src, path.join(tesseractDir, name));
    } catch {
      // Optional core variant not shipped in this package version.
    }
  }

  const engGz = path.join(tessdataDir, "eng.traineddata.gz");
  try {
    await fs.access(engGz);
    console.log("eng.traineddata.gz already present");
  } catch {
    console.log("Downloading eng.traineddata.gz for local OCR...");
    const response = await fetch(ENG_DATA_URL);
    if (!response.ok) {
      throw new Error(`Failed to download eng.traineddata.gz (${response.status})`);
    }
    const buffer = Buffer.from(await response.arrayBuffer());
    await fs.writeFile(engGz, buffer);
    console.log("Saved eng.traineddata.gz");
  }

  console.log("Tesseract browser assets ready in public/tesseract and public/tessdata");
};

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
