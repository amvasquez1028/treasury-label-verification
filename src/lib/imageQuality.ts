export type ImageQualityWarning = {
  id: string;
  message: string;
};

export const assessImageQuality = (file: File): Promise<ImageQualityWarning[]> => {
  return new Promise((resolve) => {
    const warnings: ImageQualityWarning[] = [];

    if (file.size > 5 * 1024 * 1024) {
      warnings.push({
        id: "size",
        message: `${file.name} exceeds 5 MB and will be downscaled before upload.`,
      });
    }

    if (!file.type.match(/^image\/(jpeg|png)$/i)) {
      warnings.push({
        id: "format",
        message: `${file.name} is not JPEG or PNG. Convert before uploading.`,
      });
      resolve(warnings);
      return;
    }

    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      if (img.width < 400 || img.height < 400) {
        warnings.push({
          id: "small",
          message: `${file.name} may be too small (${img.width}×${img.height}px). Label text could be hard to read.`,
        });
      }

      const aspect = img.height / Math.max(1, img.width);
      if (aspect > 2.5 || aspect < 0.4) {
        warnings.push({
          id: "aspect",
          message: `${file.name} has an unusual aspect ratio. Crop to the label if possible.`,
        });
      }

      resolve(warnings);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      resolve(warnings);
    };
    img.src = url;
  });
};

export const assessFilesQuality = async (files: File[]): Promise<ImageQualityWarning[]> => {
  const all = await Promise.all(files.map(assessImageQuality));
  return all.flat();
};
