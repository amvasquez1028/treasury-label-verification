import type { VerificationOutcome, VerificationResult } from "@/lib/api";
import { formatFieldName } from "@/lib/formatFieldName";

export type HistoryEntry = {
  id: string;
  timestamp: string;
  brandName: string;
  overallStatus: VerificationOutcome | "Failed";
  verificationResult: string;
  confidenceScore: number;
  reasoning: string;
};

const STORAGE_KEY = "label-verification-history";
const MAX_ENTRIES = 200;

const formatStatusLabel = (status: VerificationOutcome | "Failed"): string => {
  switch (status) {
    case "pass":
      return "Pass";
    case "review":
      return "Review";
    case "fail":
      return "Fail";
    case "timeout":
      return "Timeout";
    case "unreadable":
      return "Unreadable";
    case "Failed":
      return "Failed";
    default:
      const unreachable: never = status;
      return unreachable;
  }
};

const buildReasoning = (result: VerificationResult): string => {
  if (result.overallStatus === "unreadable") {
    const parts = [result.statusMessage, result.agentGuidance].filter(Boolean);
    return parts.length > 0 ? parts.join(" — ") : "Image unreadable — request a clearer label.";
  }

  const fieldNotes = result.fields
    .map((field) => {
      const matchLabel = field.isMatch ? "pass" : "fail";
      const note = field.notes ? ` — ${field.notes}` : "";
      return `${formatFieldName(field.fieldName)}: ${matchLabel} (${Math.round(field.confidence * 100)}%)${note}`;
    })
    .join("; ");

  return fieldNotes || "No field-level details available.";
};

export const saveVerificationToHistory = (
  brandName: string,
  result: VerificationResult,
  fileName?: string,
): void => {
  if (typeof window === "undefined") {
    return;
  }

  const entry: HistoryEntry = {
    id: crypto.randomUUID(),
    timestamp: new Date().toISOString(),
    brandName: fileName ? `${brandName} (${fileName})` : brandName,
    overallStatus: result.overallStatus,
    verificationResult: formatStatusLabel(result.overallStatus),
    confidenceScore: result.overallConfidence,
    reasoning: buildReasoning(result),
  };

  const existing = getVerificationHistory();
  const updated = [entry, ...existing].slice(0, MAX_ENTRIES);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
};

export const saveBatchFailureToHistory = (
  brandName: string,
  fileName: string,
  error: string,
): void => {
  if (typeof window === "undefined") {
    return;
  }

  const entry: HistoryEntry = {
    id: crypto.randomUUID(),
    timestamp: new Date().toISOString(),
    brandName: `${brandName} (${fileName})`,
    overallStatus: "Failed",
    verificationResult: "Failed",
    confidenceScore: 0,
    reasoning: error,
  };

  const existing = getVerificationHistory();
  const updated = [entry, ...existing].slice(0, MAX_ENTRIES);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
};

export const getVerificationHistory = (): HistoryEntry[] => {
  if (typeof window === "undefined") {
    return [];
  }

  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw) as HistoryEntry[];
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.map((entry) => ({
      ...entry,
      overallStatus: entry.overallStatus ?? inferStatusFromLegacy(entry),
      verificationResult:
        entry.verificationResult ??
        formatStatusLabel(entry.overallStatus ?? inferStatusFromLegacy(entry)),
    }));
  } catch {
    return [];
  }
};

const inferStatusFromLegacy = (entry: HistoryEntry): VerificationOutcome | "Failed" => {
  if (entry.verificationResult === "Failed") {
    return "Failed";
  }

  if (entry.verificationResult === "Verified") {
    return "pass";
  }

  return "fail";
};

export const formatHistoryTimestamp = (iso: string): string => {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
};
