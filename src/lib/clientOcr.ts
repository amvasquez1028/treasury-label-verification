type OcrWorker = {
  recognize: (image: File | Blob) => Promise<{ data: { text: string } }>;
  setParameters: (params: Record<string, string>) => Promise<unknown>;
  terminate: () => Promise<unknown>;
};

let workerPromise: Promise<OcrWorker> | null = null;

const ocrAssetPaths = {
  workerPath: "/tesseract/worker.min.js",
  langPath: "/tessdata",
  corePath: "/tesseract",
};

const MAX_OCR_DIMENSION = 1400;

const resetWorker = async (): Promise<void> => {
  if (!workerPromise) {
    return;
  }

  try {
    const worker = await workerPromise;
    await worker.terminate();
  } catch {
    // Worker may already be terminated.
  }

  workerPromise = null;
};

const getOcrWorker = async (): Promise<OcrWorker> => {
  if (!workerPromise) {
    workerPromise = (async () => {
      const { createWorker } = await import("tesseract.js");
      const worker = await createWorker("eng", 1, {
        workerPath: ocrAssetPaths.workerPath,
        langPath: ocrAssetPaths.langPath,
        corePath: ocrAssetPaths.corePath,
        gzip: true,
      });
      return worker;
    })();
  }

  return workerPromise;
};

export const warmOcrWorker = (): void => {
  void getOcrWorker().catch(() => undefined);
};

const enhanceCanvasForOcr = (
  context: CanvasRenderingContext2D,
  width: number,
  height: number,
): void => {
  const imageData = context.getImageData(0, 0, width, height);
  const { data } = imageData;

  for (let i = 0; i < data.length; i += 4) {
    const gray = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    const contrast = gray < 128 ? Math.max(0, gray * 0.75) : Math.min(255, gray * 1.15 + 20);
    const value = contrast < 145 ? 0 : 255;
    data[i] = value;
    data[i + 1] = value;
    data[i + 2] = value;
  }

  context.putImageData(imageData, 0, 0);
};

const renderImageToBlob = async (
  file: File,
  options: { binarize: boolean },
): Promise<Blob> => {
  if (typeof createImageBitmap !== "function") {
    return file;
  }

  const bitmap = await createImageBitmap(file);
  const largestSide = Math.max(bitmap.width, bitmap.height);
  const scale = largestSide > MAX_OCR_DIMENSION ? MAX_OCR_DIMENSION / largestSide : 1;
  const width = Math.round(bitmap.width * scale);
  const height = Math.round(bitmap.height * scale);
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext("2d");
  if (!context) {
    bitmap.close();
    return file;
  }

  context.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();
  if (options.binarize) {
    enhanceCanvasForOcr(context, width, height);
  }

  const blob = await new Promise<Blob | null>((resolve) => {
    canvas.toBlob(resolve, "image/png");
  });

  return blob ?? file;
};

/** Server OCR uses full-resolution artwork; client binarization/downscale destroys ODP flats. */
export const prepareImageForServerUpload = async (file: File): Promise<File> => file;

const prepareImageForClientOcr = async (file: File): Promise<File> => {
  const blob = await renderImageToBlob(file, { binarize: true });
  if (blob === file) {
    return file;
  }

  return new File([blob], file.name.replace(/\.[^.]+$/, "") + ".png", {
    type: "image/png",
  });
};

/** @deprecated Use prepareImageForServerUpload for verify uploads. */
export const prepareImageForVerification = prepareImageForServerUpload;

const recognizeWithPsm = async (file: File | Blob, psm: string): Promise<string> => {
  const worker = await getOcrWorker();
  await worker.setParameters({
    tessedit_pageseg_mode: psm,
  });
  const result = await worker.recognize(file);
  return result.data.text ?? "";
};

const recognizeWithWorker = async (file: File): Promise<string> => {
  const passes = await Promise.all([
    recognizeWithPsm(file, "6"),
    recognizeWithPsm(file, "3"),
    recognizeWithPsm(file, "11"),
  ]);

  const lines = passes
    .flatMap((text) => text.split("\n"))
    .map((line) => line.trim())
    .filter((line) => line.length > 1);

  return [...new Set(lines)].join("\n");
};

export const extractTextFromImageFile = async (file: File): Promise<string> => {
  try {
    const prepared = await prepareImageForClientOcr(file);
    return await recognizeWithWorker(prepared);
  } catch (error) {
    await resetWorker();
    throw error;
  }
};

export const extractTextFromImageFiles = async (files: File[]): Promise<string[]> => {
  const texts: string[] = [];

  for (const file of files) {
    texts.push(await extractTextFromImageFile(file));
  }

  return texts;
};

export const terminateOcrWorker = async (): Promise<void> => {
  await resetWorker();
};
