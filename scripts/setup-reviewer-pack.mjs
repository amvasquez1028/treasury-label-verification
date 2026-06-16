import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { downloadPdfEmbeddedLabelPagesPng } from "./lib/pdfFirstPageToPng.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const colasDir = path.join(root, "testdata", "colas");
const publicSamplesDir = path.join(root, "public", "samples");
const apiSamplesDir = path.join(root, "backend", "LabelVerification.Api", "wwwroot", "samples");
const reviewerPackDir = path.join(root, "testdata", "reviewer-pack");

const readMeta = async (ttbId) => {
  const metaPath = path.join(colasDir, `${ttbId}.meta.json`);
  const raw = await fs.readFile(metaPath, "utf8");
  return JSON.parse(raw);
};

/** Five demo samples: 2 mismatch bottle photos + 3 Texas ODP approved label images. */
const demoSamples = [
  {
    file: "01-mismatch-act-of-treason.png",
    ttbId: "21194001000323",
    label: "Act of Treason — real bottle photo (photo ≠ Texas ODP File Link product)",
    sampleKind: "mismatch_bottle_photo",
    sourceColaImage: "21194001000323.png",
  },
  {
    file: "02-mismatch-juniper-tree-gin.png",
    ttbId: "18055001000023",
    label: "The Juniper Tree Gin — real bottle photo (photo ≠ Texas ODP File Link product)",
    sampleKind: "mismatch_bottle_photo",
    sourceColaImage: "18055001000023.png",
  },
  {
    file: "03-odp-ambhar-plata.png",
    ttbId: "14106001000237",
    label: "Ambhar Plata Tequila — Texas ODP approved label (File Link)",
    sampleKind: "odp_approved_label",
  },
  {
    file: "04-odp-la-venenosa-raicilla.png",
    ttbId: "14086001000323",
    label: "La Venenosa Raicilla — Texas ODP approved label (File Link)",
    sampleKind: "odp_approved_label",
  },
  {
    file: "05-odp-jack-daniels-old-no7.png",
    ttbId: "13343001000271",
    label: "Jack Daniel's Old No. 7 — Texas ODP approved label (File Link)",
    sampleKind: "odp_approved_label",
  },
];

/** 0-based embedded-JPEG indices from Texas ODP File Link PDFs (skip certificate page 0). */
const odpSampleConfig = {
  "14106001000237": {
    jpegPageIndices: [1, 2],
    usePdfRender: true,
    renderScale: 4,
    fieldOverrides: {
      bottlerProducerAddress: "Imported by Ambhar Global Spirits, LLC",
    },
  },
  "14086001000323": {
    jpegPageIndices: [1, 2],
    fieldOverrides: {
      netContents: "750 mL",
      abvPercent: 42,
      bottlerProducerAddress: "Imported by Fidencio Spirits",
    },
  },
  "13343001000271": {
    jpegPageIndices: [0, 1, 2],
    jpegPageCrops: {
      0: { top: 0.34, bottom: 0.58 },
    },
    fieldOverrides: {
      netContents: "375 mL",
      classTypeDesignation: "Whiskey",
      bottlerProducerAddress: "Jack Daniel Distillery, Lynchburg, Tenn. USA",
      fancifulName: "Old No. 7 Brand",
      countryOfOrigin: "United States",
    },
  },
};

const cleanSamplePngs = async (dir) => {
  await fs.mkdir(dir, { recursive: true });
  for (const entry of await fs.readdir(dir)) {
    if (entry.endsWith(".png") || entry === "manifest.json") {
      await fs.unlink(path.join(dir, entry));
    }
  }
};

const copyToDirs = async (fileName, bufferOrPath) => {
  for (const dir of [publicSamplesDir, apiSamplesDir, reviewerPackDir]) {
    const dest = path.join(dir, fileName);
    if (Buffer.isBuffer(bufferOrPath)) {
      await fs.writeFile(dest, bufferOrPath);
    } else {
      await fs.copyFile(bufferOrPath, dest);
    }
  }
};

await cleanSamplePngs(publicSamplesDir);
await cleanSamplePngs(apiSamplesDir);
await cleanSamplePngs(reviewerPackDir);

const manifest = [];

for (const sample of demoSamples) {
  const meta = await readMeta(sample.ttbId);
  const fileLink = meta.tabcApprovedLabelFileLink ?? meta.tabcLabelPdfUrl;

  if (sample.sampleKind === "mismatch_bottle_photo") {
    const sourcePath = path.join(colasDir, sample.sourceColaImage);
    await fs.access(sourcePath);
    await copyToDirs(sample.file, sourcePath);
    console.log(`Mismatch bottle photo → ${sample.file} (${sample.ttbId})`);
  } else if (sample.sampleKind === "odp_approved_label") {
    if (!fileLink) {
      throw new Error(`Missing Texas ODP File Link for TTB ${sample.ttbId}`);
    }

    const tempPath = path.join(publicSamplesDir, sample.file);
    const odpConfig = odpSampleConfig[sample.ttbId];
    await downloadPdfEmbeddedLabelPagesPng(
      fileLink,
      tempPath,
      odpConfig?.jpegPageIndices ?? null,
      odpConfig?.jpegPageCrops ?? null,
      {
        usePdfRender: odpConfig?.usePdfRender ?? false,
        renderScale: odpConfig?.renderScale ?? 3,
      },
    );
    const pngBuffer = await fs.readFile(tempPath);
    await copyToDirs(sample.file, pngBuffer);
    console.log(`Texas ODP File Link (embedded image) → ${sample.file} (${sample.ttbId})`);
  }

  const odpOverrides = odpSampleConfig[sample.ttbId]?.fieldOverrides ?? {};
  const expectedLabelFields =
    sample.sampleKind === "odp_approved_label" && meta.expectedLabelFields
      ? {
          ...meta.expectedLabelFields,
          ...odpOverrides,
          labelPresentation: "fullLabel",
        }
      : (meta.expectedLabelFields ?? null);

  manifest.push({
    file: sample.file,
    ttbId: sample.ttbId,
    label: sample.label,
    sampleKind: sample.sampleKind,
    fancifulName: meta.fancifulName ?? null,
    expectedLabelFields,
    expectVerificationPass: sample.sampleKind === "odp_approved_label" ? true : false,
    approvedLabelSearchUrl: meta.approvedLabelSearchUrl ?? null,
    tabcApprovedLabelFileLink: fileLink ?? null,
    userPhotoMatchesTabcRow: meta.userPhotoMatchesTabcRow ?? null,
  });
}

const manifestJson = `${JSON.stringify(manifest, null, 2)}\n`;

for (const dir of [publicSamplesDir, apiSamplesDir, reviewerPackDir]) {
  await fs.writeFile(path.join(dir, "manifest.json"), manifestJson);
}

await fs.writeFile(
  path.join(reviewerPackDir, "README.json"),
  `${JSON.stringify(
    {
      description:
        "Five demo samples for Load samples: 2 mismatch bottle photos and 3 Texas ODP Approved Product Label Search File Link embedded label images.",
      approvedLabelSearchUrl:
        "https://data.texas.gov/dataset/Approved-Product-Label-Search/2cjh-3vae/data_preview",
      items: manifest,
    },
    null,
    2,
  )}\n`,
);

console.log(`Demo samples: ${manifest.length} files → public/samples/ and API wwwroot/samples/`);
