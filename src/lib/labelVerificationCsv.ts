import type { ExpectedLabelFields, ProductCategory } from "@/lib/api";
import { cloneExpectedLabelFields } from "@/lib/defaultExpected";
import { fetchSampleFileIfAvailable } from "@/lib/sampleManifest";

export const LABEL_VERIFICATION_CSV_TEMPLATE_PATH =
  "/templates/label-verification-manifest.csv";

export const LABEL_VERIFICATION_CSV_COLUMNS = [
  "labelImage",
  "brandName",
  "fancifulName",
  "classTypeDesignation",
  "abvPercent",
  "netContents",
  "bottlerProducerAddress",
  "countryOfOrigin",
  "productCategory",
  "labelPresentation",
  "ttbWarningText",
  "boldWarningPhrase",
] as const;

export type LabelVerificationCsvRow = {
  labelImage: string;
  expected: ExpectedLabelFields;
  fancifulName: string;
};

const HEADER_ALIASES: Record<string, keyof LabelVerificationCsvRow | keyof ExpectedLabelFields> = {
  labelimage: "labelImage",
  image: "labelImage",
  filename: "labelImage",
  file: "labelImage",
  brandname: "brandName",
  fancifulname: "fancifulName",
  classtypedesignation: "classTypeDesignation",
  class: "classTypeDesignation",
  abvpercent: "abvPercent",
  abv: "abvPercent",
  netcontents: "netContents",
  bottlerproduceraddress: "bottlerProducerAddress",
  address: "bottlerProducerAddress",
  countryoforigin: "countryOfOrigin",
  productcategory: "productCategory",
  labelpresentation: "labelPresentation",
  ttbwarningtext: "ttbWarningText",
  warningtext: "ttbWarningText",
  boldwarningphrase: "boldWarningPhrase",
  boldphrase: "boldWarningPhrase",
};

const parseCsvRecords = (text: string): string[][] => {
  const records: string[][] = [];
  let row: string[] = [];
  let field = "";
  let inQuotes = false;

  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];

    if (inQuotes) {
      if (char === '"') {
        if (text[index + 1] === '"') {
          field += '"';
          index += 1;
        } else {
          inQuotes = false;
        }
      } else {
        field += char;
      }
      continue;
    }

    if (char === '"') {
      inQuotes = true;
      continue;
    }

    if (char === ",") {
      row.push(field);
      field = "";
      continue;
    }

    if (char === "\n" || char === "\r") {
      if (char === "\r" && text[index + 1] === "\n") {
        index += 1;
      }
      row.push(field);
      field = "";
      if (row.some((cell) => cell.trim().length > 0)) {
        records.push(row);
      }
      row = [];
      continue;
    }

    field += char;
  }

  if (field.length > 0 || row.length > 0) {
    row.push(field);
    if (row.some((cell) => cell.trim().length > 0)) {
      records.push(row);
    }
  }

  return records;
};

const normalizeHeader = (header: string): string =>
  header.trim().replace(/^\uFEFF/, "").toLowerCase().replace(/[\s_-]+/g, "");

const parseProductCategory = (value: string): ProductCategory => {
  const normalized = value.trim().toLowerCase();
  if (normalized === "wine") {
    return "wine";
  }
  if (normalized === "beer" || normalized === "malt_beverage") {
    return "beer";
  }
  return "distilled_spirits";
};

const parseLabelPresentation = (
  value: string,
): ExpectedLabelFields["labelPresentation"] | undefined => {
  const normalized = value.trim();
  if (
    normalized === "fullLabel" ||
    normalized === "bottleFront" ||
    normalized === "realBottleFrontWithWarningCheck"
  ) {
    return normalized;
  }
  return undefined;
};

export const parseLabelVerificationCsv = (text: string): LabelVerificationCsvRow[] => {
  const records = parseCsvRecords(text);
  if (records.length === 0) {
    return [];
  }

  const headerCells = records[0].map(normalizeHeader);
  const hasHeader = headerCells.some((cell) =>
    ["labelimage", "image", "filename", "brandname", "brand"].includes(cell),
  );
  const dataRows = hasHeader ? records.slice(1) : records;

  const columnIndex = new Map<string, number>();
  if (hasHeader) {
    records[0].forEach((header, index) => {
      const alias = HEADER_ALIASES[normalizeHeader(header)];
      if (alias) {
        columnIndex.set(alias, index);
      }
    });
  }

  const readCell = (row: string[], key: string, fallbackIndex?: number): string => {
    const index = columnIndex.get(key) ?? fallbackIndex;
    if (index === undefined || index >= row.length) {
      return "";
    }
    return row[index].trim();
  };

  return dataRows
    .map((row) => {
      const expected = cloneExpectedLabelFields();
      const labelImage = hasHeader
        ? readCell(row, "labelImage")
        : (row[0]?.trim() ?? "");
      const fancifulName = hasHeader ? readCell(row, "fancifulName") : (row[2]?.trim() ?? "");

      if (hasHeader) {
        expected.brandName = readCell(row, "brandName");
        expected.classTypeDesignation = readCell(row, "classTypeDesignation");
        expected.abvPercent = Number(readCell(row, "abvPercent")) || 0;
        expected.netContents = readCell(row, "netContents");
        expected.bottlerProducerAddress = readCell(row, "bottlerProducerAddress");
        expected.countryOfOrigin = readCell(row, "countryOfOrigin") || "";
        expected.productCategory = parseProductCategory(readCell(row, "productCategory"));
        const presentation = parseLabelPresentation(readCell(row, "labelPresentation"));
        if (presentation) {
          expected.labelPresentation = presentation;
        }
        expected.ttbWarningText = readCell(row, "ttbWarningText");
        expected.boldWarningPhrase = readCell(row, "boldWarningPhrase");
      } else {
        expected.brandName = row[1]?.trim() ?? "";
      }

      return {
        labelImage,
        expected,
        fancifulName,
      };
    })
    .filter((item) => item.labelImage || item.expected.brandName);
};

export const imageMatchesLabelName = (fileName: string, labelImage: string): boolean => {
  const normalize = (value: string) => value.trim().toLowerCase();
  const file = normalize(fileName);
  const expected = normalize(labelImage);
  if (!expected) {
    return false;
  }
  return file === expected || file.endsWith(expected) || expected.endsWith(file);
};

export const readTextFromFile = (file: File): Promise<string> =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string") {
        resolve(reader.result);
        return;
      }
      reject(new Error("Could not decode CSV file as text."));
    };
    reader.onerror = () => {
      reject(reader.error ?? new Error("Could not read CSV file."));
    };
    reader.readAsText(file);
  });

export const promptForLabelImages = (): Promise<File[]> =>
  new Promise((resolve) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "image/png,image/jpeg";
    input.multiple = true;
    input.className = "sr-only";
    input.setAttribute("aria-hidden", "true");

    let settled = false;
    const finish = (files: File[]) => {
      if (settled) {
        return;
      }
      settled = true;
      input.remove();
      resolve(files);
    };

    input.addEventListener("change", () => {
      finish(Array.from(input.files ?? []));
    });
    input.addEventListener("cancel", () => {
      finish([]);
    });

    document.body.appendChild(input);
    input.click();
  });

export const resolveLabelImageFile = async (
  labelImage: string,
  localFiles: File[],
): Promise<File | null> => {
  const trimmed = labelImage.trim();
  if (!trimmed) {
    return null;
  }

  const localMatch = localFiles.find((file) => imageMatchesLabelName(file.name, trimmed));
  if (localMatch) {
    return localMatch;
  }

  return fetchSampleFileIfAvailable(trimmed);
};
