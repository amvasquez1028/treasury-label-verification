import type { FieldVerificationResult } from "@/lib/api";

type UnreadableResultPanelProps = {
  statusMessage?: string | null;
  agentGuidance?: string | null;
  rawOcrText?: string;
  fields?: FieldVerificationResult[];
};

export const UnreadableResultPanel = ({
  statusMessage,
  agentGuidance,
  rawOcrText,
  fields = [],
}: UnreadableResultPanelProps) => {
  const trimmedOcr = rawOcrText?.trim() ?? "";
  const partialFields = fields.filter((f) => f.extractedValue || f.confidence > 0);

  return (
    <div
      className="mt-4 rounded-md border border-[var(--color-base-lighter)] bg-[var(--color-base-lightest)] p-4"
      role="status"
      aria-label="Label image unreadable"
    >
      <h5 className="text-base font-bold text-[var(--color-primary-darker)]">
        Image unreadable — request a clearer file
      </h5>
      {statusMessage ? (
        <p className="mt-2 text-sm text-[var(--color-base-darkest)]">{statusMessage}</p>
      ) : null}
      {agentGuidance ? (
        <p className="mt-2 text-sm font-semibold text-[var(--color-base-darkest)]">{agentGuidance}</p>
      ) : null}
      <p className="mt-3 text-sm text-[var(--color-base-darkest)]">
        Ask the applicant for a clearer label image. Partial extraction is shown below when available.
      </p>

      {partialFields.length > 0 ? (
        <div className="mt-4">
          <p className="text-sm font-semibold text-[var(--color-primary-darker)]">
            Partial field extraction
          </p>
          <ul className="mt-2 space-y-1 text-sm text-[var(--color-base-darkest)]">
            {partialFields.map((field) => (
              <li key={field.fieldName}>
                <span className="font-semibold">{field.fieldName}:</span>{" "}
                {field.extractedValue ?? "—"} ({Math.round(field.confidence * 100)}%)
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className="mt-4">
        <p className="text-sm font-semibold text-[var(--color-primary-darker)]">OCR text extracted</p>
        {trimmedOcr.length > 0 ? (
          <pre
            className="mt-2 max-h-40 overflow-auto rounded border border-[var(--color-base-lighter)] bg-white p-3 text-xs whitespace-pre-wrap text-[var(--color-base-darkest)]"
            aria-label="Partial OCR text extracted from label"
          >
            {trimmedOcr}
          </pre>
        ) : (
          <p className="mt-2 text-sm text-[var(--color-base-dark)]">
            No readable characters were detected in the image.
          </p>
        )}
      </div>
    </div>
  );
};
