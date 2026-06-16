"use client";

import { useState } from "react";
import { ConfidenceScore } from "@/components/ConfidenceScore";
import type { FieldVerificationResult } from "@/lib/api";
import { formatFieldName } from "@/lib/formatFieldName";

type FieldResultDetailProps = {
  field: FieldVerificationResult;
};

const WARNING_FIELDS = new Set([
  "TtbWarningText",
  "BoldWarningPhrase",
  "WarningPlacement",
  "BoldWarningTypography",
  "WarningContrast",
]);

const describeConfidenceBand = (value: number): string => {
  const percent = Math.round(value * 100);
  if (value >= 0.9) {
    return `${percent}% — high confidence (green band)`;
  }

  if (value >= 0.6) {
    return `${percent}% — review recommended (yellow band); a fail can still show yellow when OCR is partial`;
  }

  return `${percent}% — low OCR confidence (red band); verify by eye or request a clearer image`;
};

export const FieldResultDetail = ({ field }: FieldResultDetailProps) => {
  const [expanded, setExpanded] = useState(!field.isMatch);

  const handleToggle = () => {
    setExpanded((current) => !current);
  };

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      handleToggle();
    }
  };

  const showInlineCommentary = !field.isMatch && WARNING_FIELDS.has(field.fieldName);

  return (
    <li className="border-b border-[var(--color-base-lighter)] pb-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <button
          type="button"
          onClick={handleToggle}
          onKeyDown={handleKeyDown}
          className="text-left text-sm font-semibold text-[var(--color-primary-darker)] hover:underline focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-[var(--color-primary-dark)]"
          aria-expanded={expanded}
        >
          {formatFieldName(field.fieldName)}
          {!field.isMatch ? (
            <span className="ml-2 font-normal text-[var(--color-verdict-red)]">fail</span>
          ) : null}
        </button>
        <ConfidenceScore value={field.confidence} />
      </div>
      {showInlineCommentary ? (
        <p className="mt-1 text-xs text-[var(--color-base-dark)]">
          <span className="font-semibold">Confidence:</span> {describeConfidenceBand(field.confidence)}
          {field.notes ? (
            <>
              {" "}
              <span className="font-semibold">Note:</span> {field.notes}
            </>
          ) : null}
        </p>
      ) : null}
      {expanded ? (
        <div className="mt-2 space-y-1 text-xs text-[var(--color-base-dark)]">
          <p>
            <span className="font-semibold">Status:</span>{" "}
            {field.isMatch ? "pass" : "fail"}
          </p>
          {!showInlineCommentary ? (
            <p>
              <span className="font-semibold">Confidence band:</span>{" "}
              {describeConfidenceBand(field.confidence)}
            </p>
          ) : null}
          {field.expectedValue ? (
            <p>
              <span className="font-semibold">Expected:</span> {field.expectedValue}
            </p>
          ) : null}
          {field.extractedValue ? (
            <p>
              <span className="font-semibold">Extracted:</span> {field.extractedValue}
            </p>
          ) : null}
          {field.notes && !showInlineCommentary ? (
            <p>
              <span className="font-semibold">Reasoning:</span> {field.notes}
            </p>
          ) : null}
        </div>
      ) : null}
    </li>
  );
};
