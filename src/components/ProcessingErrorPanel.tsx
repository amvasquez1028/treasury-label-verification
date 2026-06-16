type ProcessingErrorPanelProps = {
  error: string;
};

const isOcrUnavailableError = (message: string): boolean =>
  /OCR not available|cgo|tesseract/i.test(message);

const ocrSetupTips = [
  "Install Tesseract OCR and ensure tessdata is available on the server.",
  "On Windows, build the API with CGO enabled and a C compiler (for example MinGW).",
  "Set TESSDATA_PATH to your tessdata directory if it is not in the default location.",
  "Restart the API after installing Tesseract or changing environment variables.",
];

export const ProcessingErrorPanel = ({ error }: ProcessingErrorPanelProps) => {
  const ocrUnavailable = isOcrUnavailableError(error);

  return (
    <div
      className="mt-4 rounded-md border border-[var(--color-base-lighter)] bg-[var(--color-base-lightest)] p-4"
      role="alert"
      aria-label="Verification processing error"
    >
      <h5 className="text-base font-bold text-[var(--color-primary-darker)]">
        {ocrUnavailable ? "OCR not available in this build" : "Verification could not complete"}
      </h5>
      <p className="mt-2 text-sm text-[var(--color-base-darkest)]">{error}</p>
      {ocrUnavailable ? (
        <>
          <p className="mt-3 text-sm text-[var(--color-base-darkest)]">
            This preview build was compiled without CGO and Tesseract support, so label images cannot
            be processed. Field matching and confidence scores require a server with OCR enabled.
          </p>
          <ul className="mt-3 list-disc space-y-1 pl-5 text-sm text-[var(--color-base-darkest)]">
            {ocrSetupTips.map((tip) => (
              <li key={tip}>{tip}</li>
            ))}
          </ul>
        </>
      ) : (
        <p className="mt-3 text-sm text-[var(--color-base-darkest)]">
          Retry with a different image or check server logs for details. No confidence score is
          available for this run.
        </p>
      )}
    </div>
  );
};
