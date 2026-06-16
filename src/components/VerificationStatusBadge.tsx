import type { VerificationOutcome } from "@/lib/api";

export type DisplayVerificationStatus = VerificationOutcome | "Failed" | "processing_error";

type VerificationStatusBadgeProps = {
  status: DisplayVerificationStatus;
};

const statusStyles: Record<DisplayVerificationStatus, string> = {
  pass: "verdict-badge-pass",
  fail: "verdict-badge-fail",
  review: "verdict-badge-review",
  timeout: "verdict-badge-fail",
  unreadable: "verdict-badge-unreadable",
  Failed: "verdict-badge-fail",
  processing_error: "verdict-badge-unreadable",
};

const statusLabels: Record<DisplayVerificationStatus, string> = {
  pass: "Pass",
  fail: "Fail",
  review: "Review",
  timeout: "Timeout",
  unreadable: "Unreadable",
  Failed: "Failed",
  processing_error: "Processing error",
};

export const VerificationStatusBadge = ({ status }: VerificationStatusBadgeProps) => {
  const normalized = status.toLowerCase() as DisplayVerificationStatus;
  const label = statusLabels[normalized] ?? status;

  return (
    <span
      className={`inline-flex items-center rounded-md border px-3 py-1 text-sm font-semibold ${statusStyles[normalized]}`}
      aria-label={`Verification status: ${label}`}
    >
      {label}
    </span>
  );
};
