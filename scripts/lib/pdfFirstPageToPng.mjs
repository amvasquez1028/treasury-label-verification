import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createCanvas } from "@napi-rs/canvas";
import { getDocument, GlobalWorkerOptions } from "pdfjs-dist/legacy/build/pdf.mjs";
import sharp from "sharp";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const pdfJsRoot = path.join(root, "node_modules/pdfjs-dist");

GlobalWorkerOptions.workerSrc = pathToFileURL(
  path.join(pdfJsRoot, "legacy/build/pdf.worker.mjs"),
).href;

const standardFontDataUrl = pathToFileURL(path.join(pdfJsRoot, "standard_fonts/")).href;

const extractJpegStreams = (pdfBuffer) => {
  const jpegs = [];

  for (let index = 0; index < pdfBuffer.length - 1; index += 1) {
    if (pdfBuffer[index] !== 0xff || pdfBuffer[index + 1] !== 0xd8) {
      continue;
    }

    let end = pdfBuffer.length;
    for (let marker = index + 2; marker < pdfBuffer.length - 1; marker += 1) {
      if (pdfBuffer[marker] === 0xff && pdfBuffer[marker + 1] === 0xd9) {
        end = marker + 2;
        break;
      }
    }

    jpegs.push(pdfBuffer.subarray(index, end));
    index = end - 1;
  }

  return jpegs;
};

/** Texas ODP File Link PDFs lead with a certificate page; label artwork JPEGs follow. */
export const pickOdpLabelArtworkPageIndex = (jpegCount, pageNumber = null) => {
  if (pageNumber !== null) {
    return Math.min(Math.max(pageNumber, 1), jpegCount) - 1;
  }

  if (jpegCount <= 1) {
    return 0;
  }

  if (jpegCount === 2) {
    return 1;
  }

  // cert + front + back (most 3-page spirits labels)
  return 2;
};

/** Default embedded-JPEG indices (0-based): skip certificate, stitch front + back artwork. */
export const defaultOdpLabelArtworkPageIndices = (jpegCount) => {
  if (jpegCount <= 1) {
    return [0];
  }

  if (jpegCount === 2) {
    return [1];
  }

  return [1, 2];
};

const jpegStreamToPng = async (jpegStream, crop = null) => {
  if (!crop) {
    return sharp(jpegStream).png().toBuffer();
  }

  const meta = await sharp(jpegStream).metadata();
  const width = meta.width ?? 0;
  const height = meta.height ?? 0;
  const top = Math.max(0, Math.floor(height * crop.top));
  const bottom = Math.min(height, Math.floor(height * crop.bottom));
  const cropHeight = Math.max(1, bottom - top);
  return sharp(jpegStream).extract({ left: 0, top, width, height: cropHeight }).png().toBuffer();
};

export const composeVerticalPngs = async (pngBuffers) => {
  if (pngBuffers.length === 0) {
    throw new Error("composeVerticalPngs requires at least one PNG buffer");
  }

  if (pngBuffers.length === 1) {
    return pngBuffers[0];
  }

  const metas = await Promise.all(pngBuffers.map((buffer) => sharp(buffer).metadata()));
  const width = Math.max(...metas.map((meta) => meta.width ?? 0));
  const totalHeight = metas.reduce((sum, meta) => sum + (meta.height ?? 0), 0);
  const composites = [];
  let y = 0;

  for (let index = 0; index < pngBuffers.length; index += 1) {
    composites.push({ input: pngBuffers[index], top: y, left: 0 });
    y += metas[index].height ?? 0;
  }

  return sharp({
    create: {
      width,
      height: totalHeight,
      channels: 3,
      background: { r: 255, g: 255, b: 255 },
    },
  })
    .composite(composites)
    .png()
    .toBuffer();
};

const loadPdf = async (pdfBuffer) => {
  const loadingTask = getDocument({
    data: new Uint8Array(pdfBuffer),
    useSystemFonts: true,
    standardFontDataUrl,
  });
  return loadingTask.promise;
};

/**
 * Renders a PDF page to PNG bytes.
 */
export const pdfFirstPageToPng = async (pdfBuffer, scale = 2, pageNumber = 1) => {
  const pdf = await loadPdf(pdfBuffer);
  const safePageNumber = Math.min(Math.max(pageNumber, 1), pdf.numPages);
  const page = await pdf.getPage(safePageNumber);
  const viewport = page.getViewport({ scale });
  const canvas = createCanvas(Math.ceil(viewport.width), Math.ceil(viewport.height));
  const context = canvas.getContext("2d");

  await page.render({
    canvasContext: context,
    viewport,
  }).promise;

  return canvas.toBuffer("image/png");
};

/**
 * Extracts the embedded JPEG label artwork from a Texas ODP File Link PDF.
 * TABC PDFs store each page as a high-resolution JPEG stream; this pulls that
 * raster directly instead of uploading the PDF to verification.
 */
export const pdfEmbeddedLabelToPng = async (pdfBuffer, pageNumber = null, renderScale = 3) => {
  const jpegStreams = extractJpegStreams(pdfBuffer);
  if (jpegStreams.length > 0) {
    const index = pickOdpLabelArtworkPageIndex(jpegStreams.length, pageNumber);
    return jpegStreamToPng(jpegStreams[index]);
  }

  return pdfFirstPageToPng(pdfBuffer, renderScale, pageNumber ?? 1);
};

export const pdfEmbeddedLabelPagesToPng = async (
  pdfBuffer,
  pageIndices = null,
  pageCrops = null,
  renderScale = 3,
  usePdfRender = false,
) => {
  const jpegStreams = extractJpegStreams(pdfBuffer);
  if (jpegStreams.length > 0) {
    const indices = pageIndices ?? defaultOdpLabelArtworkPageIndices(jpegStreams.length);
    const pngBuffers = await Promise.all(
      indices.map(async (index) => {
        const safeIndex = Math.min(Math.max(index, 0), jpegStreams.length - 1);
        const crop = pageCrops?.[safeIndex] ?? pageCrops?.[String(safeIndex)] ?? null;
        if (usePdfRender) {
          const rendered = await pdfFirstPageToPng(pdfBuffer, renderScale, safeIndex + 1);
          if (!crop) {
            return rendered;
          }

          const meta = await sharp(rendered).metadata();
          const width = meta.width ?? 0;
          const height = meta.height ?? 0;
          const top = Math.max(0, Math.floor(height * crop.top));
          const bottom = Math.min(height, Math.floor(height * crop.bottom));
          const cropHeight = Math.max(1, bottom - top);
          return sharp(rendered).extract({ left: 0, top, width, height: cropHeight }).png().toBuffer();
        }

        return jpegStreamToPng(jpegStreams[safeIndex], crop);
      }),
    );
    return composeVerticalPngs(pngBuffers);
  }

  const fallbackPage = pageIndices?.[0] ?? 1;
  return pdfFirstPageToPng(pdfBuffer, renderScale, fallbackPage);
};

export const downloadPdfEmbeddedLabelPng = async (pdfUrl, destPath, pageNumber = null) => {
  const response = await fetch(pdfUrl);
  if (!response.ok) {
    throw new Error(`Failed to download PDF (${response.status}): ${pdfUrl}`);
  }

  const pdfBuffer = Buffer.from(await response.arrayBuffer());
  const pngBuffer = await pdfEmbeddedLabelToPng(pdfBuffer, pageNumber);
  await fs.mkdir(path.dirname(destPath), { recursive: true });
  await fs.writeFile(destPath, pngBuffer);
  return destPath;
};

export const downloadPdfEmbeddedLabelPagesPng = async (
  pdfUrl,
  destPath,
  pageIndices = null,
  pageCrops = null,
  options = {},
) => {
  const response = await fetch(pdfUrl);
  if (!response.ok) {
    throw new Error(`Failed to download PDF (${response.status}): ${pdfUrl}`);
  }

  const pdfBuffer = Buffer.from(await response.arrayBuffer());
  const pngBuffer = await pdfEmbeddedLabelPagesToPng(
    pdfBuffer,
    pageIndices,
    pageCrops,
    options.renderScale ?? 3,
    options.usePdfRender ?? false,
  );
  await fs.mkdir(path.dirname(destPath), { recursive: true });
  await fs.writeFile(destPath, pngBuffer);
  return destPath;
};

/** @deprecated Use downloadPdfEmbeddedLabelPng. */
export const downloadPdfFirstPagePng = downloadPdfEmbeddedLabelPng;
