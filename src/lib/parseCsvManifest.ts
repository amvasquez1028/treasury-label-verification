import {
  imageMatchesLabelName,
  parseLabelVerificationCsv,
  type LabelVerificationCsvRow,
} from "@/lib/labelVerificationCsv";

/** @deprecated Use LabelVerificationCsvRow from labelVerificationCsv.ts */
export type CsvManifestRow = {
  filename: string;
  ttbId: string;
};

export const parseCsvManifest = (text: string): CsvManifestRow[] =>
  parseLabelVerificationCsv(text).map((row) => ({
    filename: row.labelImage,
    ttbId: "",
  }));

export const findManifestTtbId = (): string => "";

export { imageMatchesLabelName, parseLabelVerificationCsv, type LabelVerificationCsvRow };
