export type ProductCategory = "distilled_spirits" | "wine" | "beer";

export type VerificationOutcome = "pass" | "fail" | "review" | "timeout" | "unreadable";

export type ExpectedLabelFields = {
  brandName: string;
  fancifulName?: string;
  classTypeDesignation: string;
  abvPercent: number;
  netContents: string;
  bottlerProducerAddress: string;
  countryOfOrigin?: string | null;
  productCategory: ProductCategory;
  ttbWarningText: string;
  boldWarningPhrase: string;
  labelPresentation?: "fullLabel" | "bottleFront" | "realBottleFrontWithWarningCheck";
  appellation?: string | null;
  vintage?: string | null;
  sulfiteDeclaration?: string | null;
  organicClaim?: string | null;
  barcodeUpc?: string | null;
};

export type FieldVerificationResult = {
  fieldName: string;
  isMatch: boolean;
  confidence: number;
  extractedValue?: string | null;
  expectedValue?: string | null;
  notes?: string | null;
};

export type VerificationResult = {
  overallStatus: VerificationOutcome;
  isVerified: boolean;
  overallConfidence: number;
  fields: FieldVerificationResult[];
  rawOcrText: string;
  processingTimeMs: number;
  statusMessage?: string | null;
  agentGuidance?: string | null;
};

export type BatchVerificationResult = {
  items: Array<{
    fileName: string;
    result?: VerificationResult | null;
    error?: string | null;
  }>;
  successCount: number;
  failureCount: number;
};

const jsonHeaders = { "Content-Type": "application/json" };

const parseApiErrorMessage = (text: string, status: number): string => {
  try {
    const json = JSON.parse(text) as { error?: string; availableTtbIds?: string[] };
    if (json.error && json.availableTtbIds && json.availableTtbIds.length > 0) {
      return `${json.error}. Cached TTB IDs: ${json.availableTtbIds.join(", ")}`;
    }
    if (json.error) {
      return json.error;
    }
  } catch {
    // Response body is not JSON.
  }

  return text || `Request failed (${status})`;
};

export const apiFetch = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const response = await fetch(path, {
    ...init,
    credentials: "include",
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(parseApiErrorMessage(text, response.status));
  }

  return response.json() as Promise<T>;
};

export const login = (email: string, password: string) =>
  apiFetch<{ email: string; approvalStatus: string }>("/api/v1/auth/login", {
    method: "POST",
    headers: jsonHeaders,
    body: JSON.stringify({ email, password }),
  });

export const logout = () =>
  apiFetch<{ message: string }>("/api/v1/auth/logout", { method: "POST" });

export const getMe = () =>
  apiFetch<{ email: string; approvalStatus: string }>("/api/v1/auth/me");

export type ColaCacheFields = ExpectedLabelFields & {
  fancifulName?: string | null;
};

export const getColaExpectedFields = (ttbId: string) =>
  apiFetch<ColaCacheFields>(`/api/v1/cola/${encodeURIComponent(ttbId.trim())}/expected-fields`);

export const verifyLabel = async (
  image: File,
  expected: ExpectedLabelFields,
): Promise<VerificationResult> => {
  const form = new FormData();
  form.append("image", image);
  form.append("expected", JSON.stringify(expected));

  const perLabelMs = 45_000;
  let response: Response;
  try {
    response = await fetch("/api/v1/verify", {
      method: "POST",
      body: form,
      credentials: "include",
      signal: AbortSignal.timeout(perLabelMs),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "TimeoutError") {
      throw new Error(
        `Verification timed out after ${Math.round(perLabelMs / 1000)}s. Retry with a clearer image.`,
      );
    }

    throw error;
  }

  if (!response.ok) {
    throw new Error(await response.text());
  }

  return response.json();
};

export type VerifyBatchItem = {
  file: File;
  expected: ExpectedLabelFields;
  ttbId?: string;
  ocrText?: string;
};

export const verifyLabelsSequential = async (
  items: VerifyBatchItem[],
  onProgress?: (index: number, total: number, fileName: string) => void,
): Promise<BatchVerificationResult> => {
  const batchItems: BatchVerificationResult["items"] = [];
  let successCount = 0;
  let failureCount = 0;

  for (let index = 0; index < items.length; index++) {
    const item = items[index];
    onProgress?.(index, items.length, item.file.name);

    try {
      const result = await verifyLabel(item.file, item.expected);
      batchItems.push({ fileName: item.file.name, result, error: null });
      successCount += 1;
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Verification failed.";
      batchItems.push({ fileName: item.file.name, result: null, error: message });
      failureCount += 1;
    }
  }

  return { items: batchItems, successCount, failureCount };
};

export const verifyBatch = async (items: VerifyBatchItem[]): Promise<BatchVerificationResult> => {
  const form = new FormData();
  items.forEach((item) => form.append("images", item.file));
  form.append("expectedList", JSON.stringify(items.map((item) => item.expected)));
  if (items.some((item) => item.ocrText && item.ocrText.trim().length > 0)) {
    form.append(
      "ocrTextList",
      JSON.stringify(items.map((item) => item.ocrText ?? "")),
    );
    form.append("useClientOcr", "true");
  } else {
    form.append("ocrTextList", JSON.stringify(items.map(() => "")));
    form.append("useClientOcr", "false");
  }

  // Batch verify runs labels sequentially on the server; ODP flats need ~30s each.
  const perLabelMs = 35_000;
  const timeoutMs = Math.min(180_000, items.length * perLabelMs + 10_000);

  let response: Response;
  try {
    response = await fetch("/api/v1/verify/batch", {
      method: "POST",
      body: form,
      credentials: "include",
      signal: AbortSignal.timeout(timeoutMs),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "TimeoutError") {
      throw new Error(
        `Verification timed out after ${Math.round(timeoutMs / 1000)}s. Try fewer labels or retry.`,
      );
    }

    throw new Error(
      error instanceof Error && error.message === "Failed to fetch"
        ? "Could not reach the server. Check your connection and try again."
        : error instanceof Error
          ? error.message
          : "Verification failed.",
    );
  }

  if (!response.ok) {
    throw new Error(await response.text());
  }

  return response.json();
};

export type ExtractedLabelFields = {
  ttbId?: string | null;
  brandName?: string | null;
  fancifulName?: string | null;
  classTypeDesignation?: string | null;
  abvPercent?: number | null;
  netContents?: string | null;
  bottlerProducerAddress?: string | null;
  countryOfOrigin?: string | null;
  productCategory?: string | null;
  ttbWarningText?: string | null;
  boldWarningPhrase?: string | null;
};

export type LabelExtractionResult = {
  fields: ExtractedLabelFields;
  confidences: Array<{ fieldName: string; confidence: number; source?: string | null }>;
  rawOcrText: string;
  regionTexts: Record<string, string>;
  layout: {
    documentClass: string;
    overallConfidence: number;
    primarySource: string;
  };
  processingTimeMs: number;
};

export type AutonomousVerificationResult = {
  verification: VerificationResult;
  extraction: LabelExtractionResult;
  resolvedTtbId?: string | null;
  colaRegistryHit: boolean;
  agentGuidance?: string | null;
};

export type AutonomousBatchVerificationResult = {
  items: Array<{ fileName: string; result: AutonomousVerificationResult }>;
  successCount: number;
  failureCount: number;
};

export const verifyAutonomousBatch = async (
  items: Array<{ file: File; ttbId?: string }>,
): Promise<AutonomousBatchVerificationResult> => {
  const form = new FormData();
  items.forEach((item) => form.append("images", item.file));
  form.append("ttbIdList", JSON.stringify(items.map((item) => item.ttbId?.trim() ?? "")));

  const perLabelMs = 35_000;
  const timeoutMs = Math.min(180_000, items.length * perLabelMs + 10_000);

  const response = await fetch("/api/v1/verify/batch/autonomous", {
    method: "POST",
    body: form,
    credentials: "include",
    signal: AbortSignal.timeout(timeoutMs),
  });

  if (!response.ok) {
    throw new Error(await response.text());
  }

  return response.json();
};
