import type { ExpectedLabelFields } from "@/lib/api";

export type SampleKind = "mismatch_bottle_photo" | "odp_approved_label";

export type SampleManifestItem = {
  file: string;
  ttbId: string;
  label: string;
  sampleKind?: SampleKind;
  fancifulName?: string | null;
  expectedLabelFields?: ExpectedLabelFields | null;
};

const FALLBACK_MANIFEST: SampleManifestItem[] = [
  {
    file: "01-mismatch-act-of-treason.png",
    ttbId: "21194001000323",
    label: "Act of Treason — real bottle photo (photo ≠ Texas ODP File Link product)",
    sampleKind: "mismatch_bottle_photo",
  },
  {
    file: "02-mismatch-juniper-tree-gin.png",
    ttbId: "18055001000023",
    label: "The Juniper Tree Gin — real bottle photo (photo ≠ Texas ODP File Link product)",
    sampleKind: "mismatch_bottle_photo",
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

let cachedManifest: SampleManifestItem[] | null = null;

export const resetSampleManifestCache = (): void => {
  cachedManifest = null;
};

export const loadSampleManifest = async (): Promise<SampleManifestItem[]> => {
  if (cachedManifest) {
    return cachedManifest;
  }

  try {
    const response = await fetch(`/samples/manifest.json?v=${Date.now()}`);
    if (response.ok) {
      const data = (await response.json()) as SampleManifestItem[] | { items?: SampleManifestItem[] };
      const items = Array.isArray(data) ? data : (data.items ?? []);
      if (items.length > 0) {
        cachedManifest = items.map((item) => ({
          file: item.file,
          ttbId: item.ttbId ?? "",
          label: item.label,
          sampleKind: item.sampleKind,
          fancifulName: item.fancifulName ?? null,
          expectedLabelFields: item.expectedLabelFields ?? null,
        }));
        return cachedManifest;
      }
    }
  } catch {
    // fall back to baked-in list
  }

  cachedManifest = FALLBACK_MANIFEST;
  return cachedManifest;
};

/** @deprecated Use loadSampleManifest() for dynamic manifest from public/samples/manifest.json */
export const SAMPLE_MANIFEST = FALLBACK_MANIFEST;

export const fetchSampleFile = async (fileName: string): Promise<File> => {
  const response = await fetch(`/samples/${encodeURIComponent(fileName)}`);
  if (!response.ok) {
    throw new Error(`Sample image not found: ${fileName}. Run pnpm setup:reviewer-pack.`);
  }
  const blob = await response.blob();
  return new File([blob], fileName, { type: blob.type || "image/png" });
};

export const fetchSampleFileIfAvailable = async (fileName: string): Promise<File | null> => {
  const trimmed = fileName.trim();
  if (!trimmed) {
    return null;
  }

  try {
    const response = await fetch(`/samples/${encodeURIComponent(trimmed)}`);
    if (!response.ok) {
      return null;
    }
    const blob = await response.blob();
    return new File([blob], trimmed, { type: blob.type || "image/png" });
  } catch {
    return null;
  }
};
